using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ILicenseService, LicenseService>();

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
            ValidateIssuer = false,
            ValidateAudience = false,
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

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDashboard", policy =>
    {
        var origins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5000", "http://localhost:3000"];

        policy.WithOrigins(origins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
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

// Webhook endpoint
app.MapPost("/webhook/github", async (HttpContext context, IGitHubService gitHubService) =>
{
    // Handle GitHub webhook
    return Results.Ok();
});

var port = builder.Configuration.GetValue("Server:Port", 5000);
var host = builder.Configuration.GetValue("Server:Host", "0.0.0.0");

Log.Information("XcluadeAgent starting on {Host}:{Port}", host, port);

await app.RunAsync();
