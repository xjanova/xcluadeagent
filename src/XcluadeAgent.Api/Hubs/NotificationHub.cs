using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using XcluadeAgent.Core.Interfaces;

namespace XcluadeAgent.Api.Hubs;

/// <summary>
/// SignalR hub for real-time notifications
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("uid")?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }

        // Add to global notifications group
        await Groups.AddToGroupAsync(Context.ConnectionId, "global");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Extension methods for sending notifications via SignalR
/// </summary>
public static class NotificationHubExtensions
{
    public static async Task SendNotification(this IHubContext<NotificationHub> hub, Notification notification)
    {
        await hub.Clients.Group("global").SendAsync("Notification", new
        {
            id = notification.Id,
            type = notification.Type.ToString(),
            priority = notification.Priority.ToString(),
            title = notification.Title,
            message = notification.Message,
            details = notification.Details,
            projectId = notification.ProjectId,
            projectName = notification.ProjectName,
            actionUrl = notification.ActionUrl,
            actionText = notification.ActionText,
            timestamp = notification.CreatedAt
        });
    }

    public static async Task SendNotificationToUser(this IHubContext<NotificationHub> hub, Guid userId, Notification notification)
    {
        await hub.Clients.Group($"user_{userId}").SendAsync("Notification", new
        {
            id = notification.Id,
            type = notification.Type.ToString(),
            priority = notification.Priority.ToString(),
            title = notification.Title,
            message = notification.Message,
            details = notification.Details,
            projectId = notification.ProjectId,
            projectName = notification.ProjectName,
            actionUrl = notification.ActionUrl,
            actionText = notification.ActionText,
            timestamp = notification.CreatedAt
        });
    }

    public static async Task SendAlert(this IHubContext<NotificationHub> hub, string title, string message, string type = "info")
    {
        await hub.Clients.Group("global").SendAsync("Alert", new
        {
            title,
            message,
            type,
            timestamp = DateTime.UtcNow
        });
    }
}
