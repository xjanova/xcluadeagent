using XcluadeAgent.Core.Enums;

namespace XcluadeAgent.Core.Models;

/// <summary>
/// User account for the dashboard
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Username for login
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Password hash
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Display name
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// User role
    /// </summary>
    public UserRole Role { get; set; } = UserRole.Viewer;

    /// <summary>
    /// Is account active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last login timestamp
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Last login IP
    /// </summary>
    public string? LastLoginIp { get; set; }

    /// <summary>
    /// Failed login attempts
    /// </summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>
    /// Account locked until
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// Requires password change on next login
    /// </summary>
    public bool RequirePasswordChange { get; set; }

    /// <summary>
    /// Two-factor authentication enabled
    /// </summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>
    /// Two-factor secret key
    /// </summary>
    public string? TwoFactorSecret { get; set; }

    /// <summary>
    /// Refresh token for session renewal
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Refresh token expiration
    /// </summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// GitHub OAuth ID if linked
    /// </summary>
    public string? GitHubId { get; set; }

    /// <summary>
    /// GitHub username if linked
    /// </summary>
    public string? GitHubUsername { get; set; }

    /// <summary>
    /// Notification preferences
    /// </summary>
    public UserNotificationSettings NotificationSettings { get; set; } = new();

    /// <summary>
    /// Project access (if not admin)
    /// </summary>
    public List<Guid> ProjectAccess { get; set; } = [];

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// User notification preferences
/// </summary>
public class UserNotificationSettings
{
    public bool EmailEnabled { get; set; } = true;
    public bool NotifyOnSync { get; set; } = true;
    public bool NotifyOnError { get; set; } = true;
    public bool NotifyOnRollback { get; set; } = true;
    public bool NotifyOnAiAction { get; set; } = true;
    public string? TelegramChatId { get; set; }
    public string? DiscordWebhook { get; set; }
}

/// <summary>
/// Audit log entry
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
