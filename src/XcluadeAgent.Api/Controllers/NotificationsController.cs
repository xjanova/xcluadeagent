using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using XcluadeAgent.Api.Hubs;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Shared.DTOs;

namespace XcluadeAgent.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        IAuditLogRepository auditLogRepository,
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _auditLogRepository = auditLogRepository;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Get notification history from audit log
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<NotificationDto>>>> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] bool? unreadOnly = null)
    {
        var userId = User.FindFirst("uid")?.Value;

        // Get notifications from audit log
        var logs = await _auditLogRepository.GetRecentAsync(500);

        var notifications = logs
            .Where(l => l.Action.StartsWith("Notification") ||
                       l.Action.Contains("Sync") ||
                       l.Action.Contains("Rollback") ||
                       l.Action.Contains("Error"))
            .Select(l => new NotificationDto
            {
                Id = l.Id,
                Type = GetNotificationType(l.Action),
                TypeIcon = GetTypeIcon(l.Action),
                TypeColor = GetTypeColor(l.Action),
                Title = l.Action,
                Message = l.Details ?? l.Action,
                ProjectId = l.EntityId,
                ProjectName = l.EntityType == "Project" ? l.Details?.Split(':').FirstOrDefault() : null,
                CreatedAt = l.Timestamp,
                TimeAgo = GetTimeAgo(l.Timestamp),
                IsRead = true // TODO: Track read status per user
            })
            .ToList();

        if (!string.IsNullOrEmpty(type))
        {
            notifications = notifications.Where(n => n.Type == type).ToList();
        }

        var total = notifications.Count;
        var paged = notifications
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var response = new PagedResponse<NotificationDto>
        {
            Success = true,
            Data = paged,
            Pagination = new PaginationMeta
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                HasPrevious = page > 1,
                HasNext = page * pageSize < total
            }
        };

        return Ok(ApiResponse<PagedResponse<NotificationDto>>.Ok(response));
    }

    /// <summary>
    /// Get notification statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<NotificationStatsDto>>> GetStats()
    {
        var logs = await _auditLogRepository.GetRecentAsync(100);

        var today = DateTime.UtcNow.Date;
        var weekAgo = today.AddDays(-7);

        var stats = new NotificationStatsDto
        {
            TotalToday = logs.Count(l => l.Timestamp >= today),
            TotalThisWeek = logs.Count(l => l.Timestamp >= weekAgo),
            UnreadCount = 0, // TODO: Track unread
            ByType = logs
                .GroupBy(l => GetNotificationType(l.Action))
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return Ok(ApiResponse<NotificationStatsDto>.Ok(stats));
    }

    /// <summary>
    /// Get available notification channels
    /// </summary>
    [HttpGet("channels")]
    public ActionResult<ApiResponse<List<NotificationChannelDto>>> GetChannels()
    {
        var channels = _notificationService.GetAvailableChannels()
            .Select(c => new NotificationChannelDto
            {
                Channel = c.Channel.ToString(),
                Name = c.Name,
                Description = c.Description,
                IsConfigured = c.IsConfigured,
                IsEnabled = c.IsEnabled,
                StatusMessage = c.StatusMessage
            })
            .ToList();

        return Ok(ApiResponse<List<NotificationChannelDto>>.Ok(channels));
    }

    /// <summary>
    /// Test notification channel
    /// </summary>
    [HttpPost("channels/{channel}/test")]
    public async Task<ActionResult<ApiResponse<bool>>> TestChannel(
        string channel,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Core.Enums.NotificationChannel>(channel, true, out var notificationChannel))
        {
            return BadRequest(ApiResponse<bool>.Fail("Invalid channel"));
        }

        try
        {
            var result = await _notificationService.TestChannelAsync(notificationChannel, cancellationToken);

            if (result)
            {
                _logger.LogInformation("Test notification sent to {Channel}", channel);
                return Ok(ApiResponse<bool>.Ok(true, "Test notification sent successfully"));
            }

            return Ok(ApiResponse<bool>.Fail("Failed to send test notification"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test channel {Channel}", channel);
            return Ok(ApiResponse<bool>.Fail($"Error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Mark notification as read
    /// </summary>
    [HttpPost("{id:guid}/read")]
    public ActionResult<ApiResponse> MarkAsRead(Guid id)
    {
        // TODO: Implement read status tracking
        return Ok(ApiResponse.Ok("Marked as read"));
    }

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    [HttpPost("read-all")]
    public ActionResult<ApiResponse> MarkAllAsRead()
    {
        // TODO: Implement read status tracking
        return Ok(ApiResponse.Ok("All marked as read"));
    }

    /// <summary>
    /// Send a broadcast notification (admin only)
    /// </summary>
    [HttpPost("broadcast")]
    public async Task<ActionResult<ApiResponse>> BroadcastNotification(
        [FromBody] BroadcastNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var notification = new Notification
        {
            Type = NotificationType.Info,
            Priority = NotificationPriority.Normal,
            Title = request.Title,
            Message = request.Message
        };

        await _notificationService.SendAsync(notification, cancellationToken);

        // Also send via SignalR for real-time update
        await _hubContext.Clients.All.SendAsync("NotificationReceived", new
        {
            id = notification.Id,
            type = "Info",
            title = request.Title,
            message = request.Message,
            timestamp = DateTime.UtcNow
        }, cancellationToken);

        _logger.LogInformation("Broadcast notification sent: {Title}", request.Title);

        return Ok(ApiResponse.Ok("Notification sent"));
    }

    #region Private Methods

    private static string GetNotificationType(string action) => action switch
    {
        var a when a.Contains("Sync") && a.Contains("Completed") => "SyncCompleted",
        var a when a.Contains("Sync") && a.Contains("Failed") => "SyncFailed",
        var a when a.Contains("Sync") && a.Contains("Started") => "SyncStarted",
        var a when a.Contains("Rollback") => "Rollback",
        var a when a.Contains("Error") => "Error",
        var a when a.Contains("Warning") => "Warning",
        _ => "Info"
    };

    private static string GetTypeIcon(string action) => action switch
    {
        var a when a.Contains("Sync") && a.Contains("Completed") => "✅",
        var a when a.Contains("Sync") && a.Contains("Failed") => "❌",
        var a when a.Contains("Sync") && a.Contains("Started") => "🔄",
        var a when a.Contains("Rollback") => "⏪",
        var a when a.Contains("Error") => "🚨",
        var a when a.Contains("Warning") => "⚠️",
        _ => "ℹ️"
    };

    private static string GetTypeColor(string action) => action switch
    {
        var a when a.Contains("Completed") || a.Contains("Success") => "green",
        var a when a.Contains("Failed") || a.Contains("Error") => "red",
        var a when a.Contains("Warning") => "yellow",
        var a when a.Contains("Started") || a.Contains("Progress") => "blue",
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

    #endregion
}

public class BroadcastNotificationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
