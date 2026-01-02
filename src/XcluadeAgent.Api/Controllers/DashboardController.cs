using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Shared.Constants;
using XcluadeAgent.Shared.DTOs;

namespace XcluadeAgent.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IAiService _aiService;
    private readonly ILicenseService _licenseService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IProjectRepository projectRepository,
        ISyncHistoryRepository syncHistoryRepository,
        IAiService aiService,
        ILicenseService licenseService,
        ILogger<DashboardController> logger)
    {
        _projectRepository = projectRepository;
        _syncHistoryRepository = syncHistoryRepository;
        _aiService = aiService;
        _licenseService = licenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get dashboard overview
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<DashboardOverviewDto>>> GetOverview()
    {
        var projects = (await _projectRepository.GetAllAsync()).ToList();
        var recentHistory = (await _syncHistoryRepository.GetRecentAsync(100)).ToList();
        var licenseInfo = await _licenseService.GetLicenseInfoAsync();
        var aiAvailability = await _aiService.CheckAvailabilityAsync();

        var today = DateTime.UtcNow.Date;
        var weekAgo = today.AddDays(-7);

        var overview = new DashboardOverviewDto
        {
            TotalProjects = projects.Count,
            SyncedProjects = projects.Count(p => p.Status == ProjectStatus.Synced),
            PendingProjects = projects.Count(p => p.Status == ProjectStatus.Pending),
            FailedProjects = projects.Count(p => p.Status == ProjectStatus.Failed),
            SyncsToday = recentHistory.Count(h => h.StartedAt >= today),
            SyncsThisWeek = recentHistory.Count(h => h.StartedAt >= weekAgo),

            SystemHealth = GetSystemHealth(),
            License = MapLicenseInfo(licenseInfo),
            AiStatus = MapAiStatus(aiAvailability),

            RecentProjects = projects
                .OrderByDescending(p => p.LastSyncAt ?? p.CreatedAt)
                .Take(5)
                .Select(p => new ProjectSummaryDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Framework = p.Framework.GetDisplayName(),
                    Status = p.Status.ToString(),
                    StatusColor = GetStatusColor(p.Status),
                    LastSyncedVersion = p.LastSyncedVersion,
                    LastSyncAt = p.LastSyncAt,
                    LastSyncAgo = p.LastSyncAt.HasValue ? GetTimeAgo(p.LastSyncAt.Value) : "Never",
                    LocalPath = p.LocalPath
                })
                .ToList(),

            RecentActivity = recentHistory
                .Take(10)
                .Select(h => new ActivityDto
                {
                    Id = h.Id,
                    Type = h.IsRollback ? "Rollback" : "Sync",
                    TypeIcon = h.IsRollback ? "⏪" : "🔄",
                    TypeColor = h.Success ? "green" : "red",
                    Title = h.Success
                        ? $"{(h.IsRollback ? "Rollback" : "Sync")} completed"
                        : $"{(h.IsRollback ? "Rollback" : "Sync")} failed",
                    Description = h.Success
                        ? $"Version: {h.ToVersion}"
                        : h.ErrorMessage,
                    ProjectName = h.ProjectName,
                    ProjectId = h.ProjectId,
                    User = h.InitiatedBy,
                    Timestamp = h.CompletedAt ?? h.StartedAt,
                    TimeAgo = GetTimeAgo(h.CompletedAt ?? h.StartedAt)
                })
                .ToList()
        };

        // Calculate backup stats
        var backupDir = Path.Combine("data", "backups");
        if (Directory.Exists(backupDir))
        {
            var backupFiles = Directory.GetFiles(backupDir, "*", SearchOption.AllDirectories);
            overview.BackupCount = backupFiles.Length;
            overview.TotalBackupSize = FormatBytes(backupFiles.Sum(f => new FileInfo(f).Length));
        }

        return Ok(ApiResponse<DashboardOverviewDto>.Ok(overview));
    }

    /// <summary>
    /// Get system health status
    /// </summary>
    [HttpGet("health")]
    public ActionResult<ApiResponse<SystemHealthDto>> GetHealth()
    {
        var health = GetSystemHealth();
        return Ok(ApiResponse<SystemHealthDto>.Ok(health));
    }

    /// <summary>
    /// Get AI status
    /// </summary>
    [HttpGet("ai-status")]
    public async Task<ActionResult<ApiResponse<AiStatusDto>>> GetAiStatus()
    {
        var availability = await _aiService.CheckAvailabilityAsync();
        var status = MapAiStatus(availability);
        return Ok(ApiResponse<AiStatusDto>.Ok(status));
    }

    #region Private Methods

    private SystemHealthDto GetSystemHealth()
    {
        var health = new SystemHealthDto
        {
            Version = AppConstants.Versions.Current,
            LastCheck = DateTime.UtcNow,
            Warnings = []
        };

        try
        {
            // Disk usage
            var dataDir = new DirectoryInfo("data");
            if (dataDir.Exists)
            {
                var driveInfo = new DriveInfo(dataDir.Root.FullName);
                var usedPercent = (int)((driveInfo.TotalSize - driveInfo.AvailableFreeSpace) * 100 / driveInfo.TotalSize);
                health.DiskUsage = $"{FormatBytes(driveInfo.TotalSize - driveInfo.AvailableFreeSpace)} / {FormatBytes(driveInfo.TotalSize)}";
                health.DiskUsagePercent = usedPercent;

                if (usedPercent > 90)
                {
                    health.Warnings.Add("Disk space is critically low");
                }
                else if (usedPercent > 80)
                {
                    health.Warnings.Add("Disk space is running low");
                }
            }

            // Memory usage
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var memoryMb = process.WorkingSet64 / (1024 * 1024);
            health.MemoryUsage = $"{memoryMb} MB";

            // Uptime
            health.Uptime = GetUptime();

            // Status
            health.Status = health.Warnings.Count == 0 ? "healthy" : "warning";
            health.StatusColor = health.Warnings.Count == 0 ? "green" : "yellow";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting system health");
            health.Status = "unknown";
            health.StatusColor = "gray";
        }

        return health;
    }

    private LicenseInfoDto MapLicenseInfo(LicenseInfo info) => new()
    {
        Type = info.TypeName,
        TypeColor = LicenseTypeNames.GetBadgeColor(info.Type),
        LicensedTo = info.LicensedTo,
        ExpiresAt = info.ExpiresAt?.ToString("MMM dd, yyyy"),
        DaysRemaining = info.DaysRemaining < int.MaxValue ? info.DaysRemaining : null,
        IsExpiringSoon = info.IsExpiringSoon,
        ProjectsUsed = info.ProjectsUsed,
        ProjectsLimit = info.ProjectsLimit,
        UsersUsed = info.UsersUsed,
        UsersLimit = info.UsersLimit,
        Features = GetEnabledFeatures(info.Features)
    };

    private AiStatusDto MapAiStatus(AiAvailability availability) => new()
    {
        Enabled = availability.IsAvailable,
        IsAvailable = availability.IsAvailable,
        Provider = availability.ActiveProvider.ToString(),
        Model = availability.ProviderModel,
        HasGpu = availability.HasGpu
        // TODO: Add budget tracking
    };

    private static List<string> GetEnabledFeatures(Core.Models.LicenseFeatures features)
    {
        var enabled = new List<string>();

        if (features.Sync) enabled.Add("Sync");
        if (features.Backup) enabled.Add("Backup");
        if (features.Rollback) enabled.Add("Rollback");
        if (features.WebDashboard) enabled.Add("Dashboard");
        if (features.Cli) enabled.Add("CLI");
        if (features.MultiEnvironment) enabled.Add("Multi-Environment");
        if (features.ScheduledSync) enabled.Add("Scheduled Sync");
        if (features.AdvancedNotifications) enabled.Add("Advanced Notifications");
        if (features.AiAssistant) enabled.Add("AI Assistant");
        if (features.TeamManagement) enabled.Add("Team Management");
        if (features.ApiAccess) enabled.Add("API Access");

        return enabled;
    }

    private static string GetStatusColor(ProjectStatus status) => status switch
    {
        ProjectStatus.Synced => "green",
        ProjectStatus.Pending or ProjectStatus.Syncing => "yellow",
        ProjectStatus.Failed => "red",
        ProjectStatus.Disabled => "gray",
        _ => "gray"
    };

    private static string GetTimeAgo(DateTime time)
    {
        var diff = DateTime.UtcNow - time;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays}d ago";
        return time.ToString("MMM dd");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    private static string GetUptime()
    {
        var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
        if (uptime.TotalMinutes < 60) return $"{(int)uptime.TotalMinutes} minutes";
        if (uptime.TotalHours < 24) return $"{(int)uptime.TotalHours} hours";
        return $"{(int)uptime.TotalDays} days";
    }

    #endregion
}
