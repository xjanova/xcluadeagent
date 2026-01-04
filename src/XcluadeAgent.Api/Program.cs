using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;
using XcluadeAgent.Infrastructure.AI;
using XcluadeAgent.Infrastructure.GitHub;
using XcluadeAgent.Infrastructure.License;
using XcluadeAgent.Infrastructure.Notifications;
using XcluadeAgent.Infrastructure.Persistence;
using XcluadeAgent.Infrastructure.Scanner;
using XcluadeAgent.Infrastructure.Security;
using XcluadeAgent.Infrastructure.Sync;
using XcluadeAgent.Api.Hubs;
using XcluadeAgent.Shared.Constants;

// XcluadeAgent - GitHub Sync Service
// Developed by xman studio | https://xman4289.com

Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════════════╗
║     ██╗  ██╗ ██████╗██╗     ██╗   ██╗ █████╗ ██████╗ ███████╗     ║
║     ╚██╗██╔╝██╔════╝██║     ██║   ██║██╔══██╗██╔══██╗██╔════╝     ║
║      ╚███╔╝ ██║     ██║     ██║   ██║███████║██║  ██║█████╗       ║
║      ██╔██╗ ██║     ██║     ██║   ██║██╔══██║██║  ██║██╔══╝       ║
║     ██╔╝ ██╗╚██████╗███████╗╚██████╔╝██║  ██║██████╔╝███████╗     ║
║     ╚═╝  ╚═╝ ╚═════╝╚══════╝ ╚═════╝ ╚═╝  ╚═╝╚═════╝ ╚══════╝     ║
║                         AGENT                                      ║
║                                                                    ║
║     Developed by: xman studio                                      ║
║     Website: https://xman4289.com                                  ║
╚═══════════════════════════════════════════════════════════════════╝
");

var builder = WebApplication.CreateBuilder(args);

// Load YAML configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddYamlFile("config/config.yaml", optional: true, reloadOnChange: true)
    .AddYamlFile($"config/config.{builder.Environment.EnvironmentName}.yaml", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables("XCLUADE_")
    .AddCommandLine(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", AppConstants.AppName)
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(builder.Configuration["Logging:Path"] ?? "data/logs", "xcluade-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: builder.Configuration.GetValue("Logging:RetentionDays", 30))
    .CreateLogger();

builder.Host.UseSerilog();

// Bind configuration sections
builder.Services.Configure<AppConfig>(builder.Configuration);
builder.Services.Configure<GitHubConfig>(builder.Configuration.GetSection("GitHub"));
builder.Services.Configure<AiConfig>(builder.Configuration.GetSection("Ai"));
builder.Services.Configure<NotificationConfig>(builder.Configuration.GetSection("Notifications"));
builder.Services.Configure<SecurityConfig>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<LicenseConfig>(builder.Configuration.GetSection("License"));

// Database
var connectionString = builder.Configuration.GetValue<string>("Database:ConnectionString")
    ?? "Data Source=data/xcluadeagent.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// HTTP clients
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("GitHub", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "XcluadeAgent/1.0.0");
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
});

// Services
builder.Services.AddScoped<IGitHubService, GitHubService>();
builder.Services.AddScoped<IGitHubOAuthService, GitHubOAuthService>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ILicenseService, LicenseService>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddSingleton<ITwoFactorService, TwoFactorService>();

// Scanner services
builder.Services.AddScoped<ISystemScanner, SystemScanner>();
builder.Services.AddScoped<IWebServerDetector, WebServerDetector>();
builder.Services.AddScoped<IWebsiteDiscovery, WebsiteDiscovery>();
builder.Services.AddScoped<IPermissionManager, PermissionManager>();
builder.Services.AddScoped<IConflictResolver, ConflictResolver>();
builder.Services.AddScoped<IEnvironmentAnalyzer, EnvironmentAnalyzer>();

// Repositories
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ISyncHistoryRepository, SyncHistoryRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// Caching
builder.Services.AddMemoryCache();

// Authentication
var jwtSecret = builder.Configuration.GetValue<string>("Security:JwtSecret")
    ?? throw new InvalidOperationException("JWT secret not configured. Set Security:JwtSecret in config or XCLUADE_SECURITY__JWTSECRET environment variable.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = "XcluadeAgent",
            ValidateAudience = true,
            ValidAudience = "XcluadeAgent",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // SignalR token from query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "XcluadeAgent API",
        Version = "v1",
        Description = "GitHub Sync Service API - Developed by xman studio",
        Contact = new OpenApiContact
        {
            Name = "xman studio",
            Url = new Uri("https://xman4289.com")
        }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// SignalR
builder.Services.AddSignalR();

// CORS - Restrict to specific methods and headers
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDashboard", policy =>
    {
        var origins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5000", "http://localhost:3000"];

        policy.WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .WithHeaders("Content-Type", "Authorization", "X-Requested-With")
            .AllowCredentials();
    });
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    // Global rate limit - 100 requests per minute
    options.AddFixedWindowLimiter("global", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 10;
    });

    // Auth rate limit - 10 login attempts per minute
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });

    // Sync rate limit - 5 sync requests per minute per project
    options.AddFixedWindowLimiter("sync", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 2;
    });

    // Webhook rate limit - 30 webhook requests per minute (generous for GitHub)
    options.AddFixedWindowLimiter("webhook", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 5;
    });
});

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "XcluadeAgent API v1");
        c.RoutePrefix = "api/docs";
    });
}

app.UseStaticFiles();
app.UseCors("AllowDashboard");
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// API routes
app.MapControllers();

// SignalR hubs
app.MapHub<SyncHub>("/hubs/sync");
app.MapHub<NotificationHub>("/hubs/notifications");

// Blazor
app.MapRazorComponents<XcluadeAgent.Api.Components.App>()
    .AddInteractiveServerRenderMode();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    version = AppConstants.Versions.Current,
    timestamp = DateTime.UtcNow
}));

// Webhook endpoint for GitHub (rate limited)
app.MapPost("/webhook/github", async (HttpContext context, IGitHubService gitHubService, ISyncService syncService, IProjectRepository projectRepository, ILogger<Program> logger) =>
{
    // Log webhook request for security auditing
    var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    try
    {
        // Read the request body
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        // Get signature and event type from headers
        var signature = context.Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        var eventType = context.Request.Headers["X-GitHub-Event"].FirstOrDefault();
        var deliveryId = context.Request.Headers["X-GitHub-Delivery"].FirstOrDefault();

        if (string.IsNullOrEmpty(signature))
        {
            logger.LogWarning("Webhook received without signature. Delivery: {DeliveryId}", deliveryId);
            return Results.Unauthorized();
        }

        if (string.IsNullOrEmpty(eventType))
        {
            logger.LogWarning("Webhook received without event type. Delivery: {DeliveryId}", deliveryId);
            return Results.BadRequest(new { error = "Missing X-GitHub-Event header" });
        }

        // Parse the payload to get repository info
        var jsonDoc = System.Text.Json.JsonDocument.Parse(payload);
        var repoFullName = jsonDoc.RootElement.TryGetProperty("repository", out var repo)
            ? repo.TryGetProperty("full_name", out var name) ? name.GetString() : null
            : null;

        if (string.IsNullOrEmpty(repoFullName))
        {
            logger.LogWarning("Webhook payload missing repository info. Delivery: {DeliveryId}", deliveryId);
            return Results.BadRequest(new { error = "Missing repository information" });
        }

        // Find project by repository
        var projects = await projectRepository.GetAllAsync();
        var project = projects.FirstOrDefault(p =>
            p.Repository.Equals(repoFullName, StringComparison.OrdinalIgnoreCase));

        if (project == null)
        {
            logger.LogInformation("No project configured for repository {Repository}. Delivery: {DeliveryId}", repoFullName, deliveryId);
            return Results.Ok(new { message = "No project configured for this repository" });
        }

        // Validate webhook signature using project's webhook secret
        var webhookSecret = project.WebhookSecret;
        if (string.IsNullOrEmpty(webhookSecret))
        {
            logger.LogWarning("Project {Project} has no webhook secret configured. Delivery: {DeliveryId}", project.Name, deliveryId);
            return Results.Unauthorized();
        }

        if (!gitHubService.ValidateWebhookSignature(payload, signature, webhookSecret))
        {
            logger.LogWarning("Invalid webhook signature for project {Project}. Delivery: {DeliveryId}", project.Name, deliveryId);
            return Results.Unauthorized();
        }

        logger.LogInformation("Valid webhook received for {Project}. Event: {Event}, Delivery: {DeliveryId}",
            project.Name, eventType, deliveryId);

        // Handle specific events
        switch (eventType.ToLower())
        {
            case "release":
                var action = jsonDoc.RootElement.TryGetProperty("action", out var actionProp)
                    ? actionProp.GetString() : null;

                if (action == "published" && project.UseReleases && project.Enabled)
                {
                    logger.LogInformation("Triggering sync for project {Project} due to release publish", project.Name);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await syncService.SyncAsync(project.Id, XcluadeAgent.Core.Enums.SyncTrigger.Webhook, "webhook", default);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to sync project {Project} from webhook", project.Name);
                        }
                    });
                }
                break;

            case "push":
                if (!project.UseReleases && project.Enabled)
                {
                    var refName = jsonDoc.RootElement.TryGetProperty("ref", out var refProp)
                        ? refProp.GetString() : null;
                    var expectedRef = $"refs/heads/{project.Branch ?? "main"}";

                    if (refName == expectedRef)
                    {
                        logger.LogInformation("Triggering sync for project {Project} due to push to {Branch}",
                            project.Name, project.Branch);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await syncService.SyncAsync(project.Id, XcluadeAgent.Core.Enums.SyncTrigger.Webhook, "webhook", default);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to sync project {Project} from webhook", project.Name);
                            }
                        });
                    }
                }
                break;

            case "ping":
                logger.LogInformation("Ping webhook received for project {Project}", project.Name);
                break;

            default:
                logger.LogDebug("Unhandled webhook event {Event} for project {Project}", eventType, project.Name);
                break;
        }

        logger.LogInformation("Webhook processed successfully from {ClientIp}. Project: {Project}, Event: {Event}",
            clientIp, project.Name, eventType);
        return Results.Ok(new { message = "Webhook processed", project = project.Name, eventType });
    }
    catch (System.Text.Json.JsonException ex)
    {
        logger.LogWarning(ex, "Invalid JSON in webhook payload from {ClientIp}", clientIp);
        return Results.BadRequest(new { error = "Invalid JSON payload" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing webhook from {ClientIp}", clientIp);
        return Results.Problem("Internal server error processing webhook");
    }
}).RequireRateLimiting("webhook");

var port = builder.Configuration.GetValue("Server:Port", 5000);
var host = builder.Configuration.GetValue("Server:Host", "0.0.0.0");

Log.Information("XcluadeAgent starting on {Host}:{Port}", host, port);

await app.RunAsync();
