using Microsoft.AspNetCore.SignalR;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Worker;

/// <summary>
/// Background worker for automated sync operations
/// </summary>
public class SyncWorker : BackgroundService
{
    private readonly ILogger<SyncWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public SyncWorker(ILogger<SyncWorker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("XcluadeAgent Sync Worker starting...");

        // Initial delay to let services start up
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUpdatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in sync worker");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("XcluadeAgent Sync Worker stopping...");
    }

    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var projectRepository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var gitHubService = scope.ServiceProvider.GetRequiredService<IGitHubService>();
        var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var projects = await projectRepository.GetEnabledAsync(cancellationToken);

        foreach (var project in projects)
        {
            try
            {
                if (!project.Enabled) continue;

                // Get latest release from GitHub
                var latestRelease = await gitHubService.GetLatestReleaseAsync(
                    project.Repository,
                    project.ReleaseTagPattern,
                    cancellationToken);

                if (latestRelease == null) continue;

                // Check if we need to sync
                if (project.LastSyncedVersion != latestRelease.TagName)
                {
                    _logger.LogInformation(
                        "New version available for {Project}: {CurrentVersion} -> {NewVersion}",
                        project.Name,
                        project.LastSyncedVersion ?? "none",
                        latestRelease.TagName);

                    // Check if auto-sync is enabled
                    if (project.Config.AutoSync)
                    {
                        _logger.LogInformation("Auto-syncing project {Project} to version {Version}",
                            project.Name, latestRelease.TagName);

                        var result = await syncService.SyncAsync(
                            project.Id,
                            SyncTrigger.Scheduled,
                            "sync-worker",
                            cancellationToken);

                        if (result.Success)
                        {
                            _logger.LogInformation("Successfully synced {Project} to {Version}",
                                project.Name, latestRelease.TagName);
                        }
                        else
                        {
                            _logger.LogWarning("Sync failed for {Project}: {Error}",
                                project.Name, result.ErrorMessage);
                        }
                    }
                    else
                    {
                        // Just mark as pending and notify
                        project.Status = ProjectStatus.Pending;
                        await projectRepository.UpdateAsync(project, cancellationToken);

                        await notificationService.SendAsync(
                            NotificationTemplates.NewVersionAvailable(
                                project.Name,
                                project.LastSyncedVersion ?? "none",
                                latestRelease.TagName),
                            cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check updates for project {Project}", project.Name);
            }
        }
    }
}

/// <summary>
/// Health check worker
/// </summary>
public class HealthCheckWorker : BackgroundService
{
    private readonly ILogger<HealthCheckWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public HealthCheckWorker(ILogger<HealthCheckWorker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("XcluadeAgent Health Check Worker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformHealthChecksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health check worker");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var projectRepository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        var projects = await projectRepository.GetEnabledAsync(cancellationToken);

        foreach (var project in projects)
        {
            if (project.Config.HealthCheck?.Enabled != true) continue;
            if (string.IsNullOrEmpty(project.Config.HealthCheck.Url)) continue;

            try
            {
                using var client = httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(project.Config.HealthCheck.TimeoutSeconds);

                var response = await client.GetAsync(project.Config.HealthCheck.Url, cancellationToken);

                if ((int)response.StatusCode != project.Config.HealthCheck.ExpectedStatusCode)
                {
                    _logger.LogWarning(
                        "Health check failed for {Project}: {StatusCode}",
                        project.Name,
                        response.StatusCode);

                    await notificationService.SendAsync(
                        NotificationTemplates.HealthCheckFailed(
                            project.Name,
                            project.Config.HealthCheck.Url,
                            $"Status code: {(int)response.StatusCode}"),
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check error for {Project}", project.Name);

                await notificationService.SendAsync(
                    NotificationTemplates.HealthCheckFailed(
                        project.Name,
                        project.Config.HealthCheck.Url,
                        ex.Message),
                    cancellationToken);
            }
        }
    }
}
