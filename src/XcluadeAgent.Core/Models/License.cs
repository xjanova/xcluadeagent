using XcluadeAgent.Core.Enums;

namespace XcluadeAgent.Core.Models;

/// <summary>
/// License information for XcluadeAgent
/// </summary>
public class License
{
    /// <summary>
    /// License key
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// License type
    /// </summary>
    public LicenseType Type { get; set; } = LicenseType.Community;

    /// <summary>
    /// Licensed to (organization/person)
    /// </summary>
    public string LicensedTo { get; set; } = string.Empty;

    /// <summary>
    /// Contact email
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Issue date
    /// </summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>
    /// Expiration date (null = never expires)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Maximum number of projects
    /// </summary>
    public int MaxProjects { get; set; } = 3;

    /// <summary>
    /// Maximum number of users
    /// </summary>
    public int MaxUsers { get; set; } = 1;

    /// <summary>
    /// Features enabled by this license
    /// </summary>
    public LicenseFeatures Features { get; set; } = new();

    /// <summary>
    /// Hardware ID for binding (optional)
    /// </summary>
    public string? HardwareId { get; set; }

    /// <summary>
    /// License signature for validation
    /// </summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// Last validation timestamp
    /// </summary>
    public DateTime? LastValidatedAt { get; set; }

    /// <summary>
    /// Is license currently valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation error message if invalid
    /// </summary>
    public string? ValidationError { get; set; }
}

/// <summary>
/// Features available in the license
/// </summary>
public class LicenseFeatures
{
    // Core features (all licenses)
    public bool Sync { get; set; } = true;
    public bool Backup { get; set; } = true;
    public bool Rollback { get; set; } = true;
    public bool WebDashboard { get; set; } = true;
    public bool Cli { get; set; } = true;

    // Professional features
    public bool MultiEnvironment { get; set; } = false;
    public bool ScheduledSync { get; set; } = false;
    public bool AdvancedNotifications { get; set; } = false;
    public bool SslMonitoring { get; set; } = false;
    public bool CustomBranding { get; set; } = false;

    // Enterprise features
    public bool AiAssistant { get; set; } = false;
    public bool TeamManagement { get; set; } = false;
    public bool AuditLogs { get; set; } = false;
    public bool ApiAccess { get; set; } = false;
    public bool SsoIntegration { get; set; } = false;
    public bool PrioritySupport { get; set; } = false;

    /// <summary>
    /// Get default features for license type
    /// </summary>
    public static LicenseFeatures GetDefault(LicenseType type) => type switch
    {
        LicenseType.Community => new LicenseFeatures
        {
            Sync = true,
            Backup = true,
            Rollback = true,
            WebDashboard = true,
            Cli = true
        },
        LicenseType.Professional => new LicenseFeatures
        {
            Sync = true,
            Backup = true,
            Rollback = true,
            WebDashboard = true,
            Cli = true,
            MultiEnvironment = true,
            ScheduledSync = true,
            AdvancedNotifications = true,
            SslMonitoring = true,
            CustomBranding = true
        },
        LicenseType.Enterprise or LicenseType.Unlimited => new LicenseFeatures
        {
            Sync = true,
            Backup = true,
            Rollback = true,
            WebDashboard = true,
            Cli = true,
            MultiEnvironment = true,
            ScheduledSync = true,
            AdvancedNotifications = true,
            SslMonitoring = true,
            CustomBranding = true,
            AiAssistant = true,
            TeamManagement = true,
            AuditLogs = true,
            ApiAccess = true,
            SsoIntegration = true,
            PrioritySupport = true
        },
        _ => new LicenseFeatures()
    };
}

/// <summary>
/// License limits by type
/// </summary>
public static class LicenseLimits
{
    public static (int projects, int users) GetLimits(LicenseType type) => type switch
    {
        LicenseType.Community => (3, 1),
        LicenseType.Professional => (10, 5),
        LicenseType.Enterprise => (50, 25),
        LicenseType.Unlimited => (int.MaxValue, int.MaxValue),
        _ => (3, 1)
    };
}
