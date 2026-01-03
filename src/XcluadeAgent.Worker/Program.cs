using Microsoft.EntityFrameworkCore;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;
using XcluadeAgent.Infrastructure.AI;
using XcluadeAgent.Infrastructure.GitHub;
using XcluadeAgent.Infrastructure.License;
using XcluadeAgent.Infrastructure.Notifications;
using XcluadeAgent.Infrastructure.Persistence;
using XcluadeAgent.Infrastructure.Scanner;
using XcluadeAgent.Infrastructure.Sync;
using XcluadeAgent.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Load configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables("XCLUADE_");

// Bind configuration sections
builder.Services.Configure<AppConfig>(builder.Configuration);
builder.Services.Configure<GitHubConfig>(builder.Configuration.GetSection("GitHub"));
builder.Services.Configure<AiConfig>(builder.Configuration.GetSection("Ai"));
builder.Services.Configure<NotificationConfig>(builder.Configuration.GetSection("Notifications"));
builder.Services.Configure<LicenseConfig>(builder.Configuration.GetSection("License"));

// Database
var connectionString = builder.Configuration.GetValue<string>("Database:ConnectionString")
    ?? "Data Source=data/xcluadeagent.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// HTTP clients
builder.Services.AddHttpClient();

// Services
builder.Services.AddScoped<IGitHubService, GitHubService>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ILicenseService, LicenseService>();
builder.Services.AddScoped<ISyncService, SyncService>();

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

// Register background workers
builder.Services.AddHostedService<SyncWorker>();
builder.Services.AddHostedService<HealthCheckWorker>();

var host = builder.Build();
host.Run();
