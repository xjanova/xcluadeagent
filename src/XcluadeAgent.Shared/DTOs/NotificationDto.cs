namespace XcluadeAgent.Shared.DTOs;

/// <summary>
/// Notification display DTO
/// </summary>
public class NotificationDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string TypeIcon { get; set; } = string.Empty;
    public string TypeColor { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
    public bool IsRead { get; set; }
}

/// <summary>
/// Notification statistics
/// </summary>
public class NotificationStatsDto
{
    public int TotalToday { get; set; }
    public int TotalThisWeek { get; set; }
    public int UnreadCount { get; set; }
    public Dictionary<string, int> ByType { get; set; } = new();
}

/// <summary>
/// Notification channel info
/// </summary>
public class NotificationChannelDto
{
    public string Channel { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
    public bool IsEnabled { get; set; }
    public string? StatusMessage { get; set; }
}

/// <summary>
/// Audit log DTO
/// </summary>
public class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? EntityName { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
    public string ActionColor { get; set; } = string.Empty;
    public string ActionIcon { get; set; } = string.Empty;
}

/// <summary>
/// Audit statistics
/// </summary>
public class AuditStatsDto
{
    public int TotalLogs { get; set; }
    public int TodayLogs { get; set; }
    public int WeekLogs { get; set; }
    public Dictionary<string, int> ByAction { get; set; } = new();
    public Dictionary<string, int> ByUser { get; set; } = new();
    public Dictionary<string, int> ByEntityType { get; set; } = new();
    public List<RecentActivityDto> RecentActivity { get; set; } = [];
}

/// <summary>
/// Recent activity item
/// </summary>
public class RecentActivityDto
{
    public string Action { get; set; } = string.Empty;
    public string? Username { get; set; }
    public DateTime Timestamp { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
}
