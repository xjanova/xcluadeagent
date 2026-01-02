using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace XcluadeAgent.Api.Hubs;

/// <summary>
/// SignalR hub for real-time sync updates
/// </summary>
[Authorize]
public class SyncHub : Hub
{
    private readonly ILogger<SyncHub> _logger;

    public SyncHub(ILogger<SyncHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("uid")?.Value;
        _logger.LogInformation("Client connected: {ConnectionId} (User: {UserId})", Context.ConnectionId, userId);

        // Join user's personal group
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to project updates
    /// </summary>
    public async Task SubscribeToProject(Guid projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"project_{projectId}");
        _logger.LogDebug("Client {ConnectionId} subscribed to project {ProjectId}", Context.ConnectionId, projectId);
    }

    /// <summary>
    /// Unsubscribe from project updates
    /// </summary>
    public async Task UnsubscribeFromProject(Guid projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project_{projectId}");
        _logger.LogDebug("Client {ConnectionId} unsubscribed from project {ProjectId}", Context.ConnectionId, projectId);
    }

    /// <summary>
    /// Subscribe to all sync updates
    /// </summary>
    public async Task SubscribeToAllSyncs()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all_syncs");
    }
}

/// <summary>
/// Extension methods for sending sync updates via SignalR
/// </summary>
public static class SyncHubExtensions
{
    public static async Task SendSyncStarted(this IHubContext<SyncHub> hub, Guid projectId, string projectName)
    {
        await hub.Clients.Group($"project_{projectId}").SendAsync("SyncStarted", new
        {
            projectId,
            projectName,
            timestamp = DateTime.UtcNow
        });

        await hub.Clients.Group("all_syncs").SendAsync("SyncStarted", new
        {
            projectId,
            projectName,
            timestamp = DateTime.UtcNow
        });
    }

    public static async Task SendSyncProgress(this IHubContext<SyncHub> hub, Guid projectId, int progress, string message)
    {
        await hub.Clients.Group($"project_{projectId}").SendAsync("SyncProgress", new
        {
            projectId,
            progress,
            message,
            timestamp = DateTime.UtcNow
        });
    }

    public static async Task SendSyncCompleted(this IHubContext<SyncHub> hub, Guid projectId, string projectName, string version, bool success, string? error = null)
    {
        var data = new
        {
            projectId,
            projectName,
            version,
            success,
            error,
            timestamp = DateTime.UtcNow
        };

        await hub.Clients.Group($"project_{projectId}").SendAsync("SyncCompleted", data);
        await hub.Clients.Group("all_syncs").SendAsync("SyncCompleted", data);
    }

    public static async Task SendLogMessage(this IHubContext<SyncHub> hub, Guid projectId, string level, string message)
    {
        await hub.Clients.Group($"project_{projectId}").SendAsync("LogMessage", new
        {
            projectId,
            level,
            message,
            timestamp = DateTime.UtcNow
        });
    }
}
