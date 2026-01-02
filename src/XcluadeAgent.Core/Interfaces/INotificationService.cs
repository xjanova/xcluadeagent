using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Core.Interfaces;

/// <summary>
/// Service for sending notifications
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Send notification to all configured channels
    /// </summary>
    Task SendAsync(Notification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send notification to specific channel
    /// </summary>
    Task SendToChannelAsync(NotificationChannel channel, Notification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send notification to specific user
    /// </summary>
    Task SendToUserAsync(Guid userId, Notification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test notification channel
    /// </summary>
    Task<bool> TestChannelAsync(NotificationChannel channel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available channels
    /// </summary>
    IEnumerable<ChannelInfo> GetAvailableChannels();
}

/// <summary>
/// Notification message
/// </summary>
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Notification type
/// </summary>
public enum NotificationType
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3,
    SyncStarted = 10,
    SyncCompleted = 11,
    SyncFailed = 12,
    RollbackStarted = 20,
    RollbackCompleted = 21,
    RollbackFailed = 22,
    AiAnalysis = 30,
    AiSuggestion = 31,
    AiFixApplied = 32,
    AiApprovalRequired = 33,
    HealthCheckFailed = 40,
    HealthCheckRecovered = 41,
    DiskSpaceLow = 50,
    SslExpiring = 51,
    LicenseExpiring = 60,
    LicenseExpired = 61,
    SecurityAlert = 70,
    SystemUpdate = 80
}

/// <summary>
/// Notification priority
/// </summary>
public enum NotificationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Channel info
/// </summary>
public class ChannelInfo
{
    public NotificationChannel Channel { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
    public bool IsEnabled { get; set; }
    public string? StatusMessage { get; set; }
}

/// <summary>
/// Notification templates
/// </summary>
public static class NotificationTemplates
{
    public static Notification SyncCompleted(string projectName, string version, int filesChanged, long durationMs) => new()
    {
        Type = NotificationType.SyncCompleted,
        Priority = NotificationPriority.Normal,
        Title = $"✅ Sync Completed: {projectName}",
        Message = $"Successfully synced to version {version}",
        Details = $"Files changed: {filesChanged}\nDuration: {durationMs}ms",
        ProjectName = projectName
    };

    public static Notification SyncFailed(string projectName, string error) => new()
    {
        Type = NotificationType.SyncFailed,
        Priority = NotificationPriority.High,
        Title = $"❌ Sync Failed: {projectName}",
        Message = $"Sync operation failed",
        Details = error,
        ProjectName = projectName
    };

    public static Notification RollbackCompleted(string projectName, string version) => new()
    {
        Type = NotificationType.RollbackCompleted,
        Priority = NotificationPriority.High,
        Title = $"⏪ Rollback Completed: {projectName}",
        Message = $"Rolled back to version {version}",
        ProjectName = projectName
    };

    public static Notification AiSuggestion(string projectName, string summary) => new()
    {
        Type = NotificationType.AiSuggestion,
        Priority = NotificationPriority.Normal,
        Title = $"🤖 AI Suggestion: {projectName}",
        Message = summary,
        ProjectName = projectName
    };

    public static Notification AiApprovalRequired(string projectName, string description) => new()
    {
        Type = NotificationType.AiApprovalRequired,
        Priority = NotificationPriority.High,
        Title = $"🤖 AI Fix Pending Approval: {projectName}",
        Message = description,
        ActionText = "Review & Approve",
        ProjectName = projectName
    };

    public static Notification HealthCheckFailed(string projectName, string url, string error) => new()
    {
        Type = NotificationType.HealthCheckFailed,
        Priority = NotificationPriority.Critical,
        Title = $"🚨 Health Check Failed: {projectName}",
        Message = $"URL: {url}",
        Details = error,
        ProjectName = projectName
    };
}
