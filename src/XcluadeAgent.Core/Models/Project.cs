using XcluadeAgent.Core.Enums;

namespace XcluadeAgent.Core.Models;

/// <summary>
/// Represents a project that syncs with GitHub
/// </summary>
public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Project display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Project description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// GitHub repository (format: owner/repo)
    /// </summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>
    /// Branch to sync from (default: main)
    /// </summary>
    public string Branch { get; set; } = "main";

    /// <summary>
    /// Use GitHub Releases instead of branch
    /// </summary>
    public bool UseReleases { get; set; } = true;

    /// <summary>
    /// Release tag pattern (e.g., "v*" for version tags)
    /// </summary>
    public string? ReleaseTagPattern { get; set; }

    /// <summary>
    /// Local path to sync files to
    /// </summary>
    public string LocalPath { get; set; } = string.Empty;

    /// <summary>
    /// Current sync status
    /// </summary>
    public ProjectStatus Status { get; set; } = ProjectStatus.Pending;

    /// <summary>
    /// Last sync timestamp
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// Last synced release/commit
    /// </summary>
    public string? LastSyncedVersion { get; set; }

    /// <summary>
    /// Last error message if failed
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Detected or configured framework type
    /// </summary>
    public FrameworkType Framework { get; set; } = FrameworkType.Unknown;

    /// <summary>
    /// Auto-detect framework type
    /// </summary>
    public bool AutoDetectFramework { get; set; } = true;

    /// <summary>
    /// Environment (dev/staging/production)
    /// </summary>
    public string Environment { get; set; } = "production";

    /// <summary>
    /// Is project enabled for sync
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Project configuration
    /// </summary>
    public ProjectConfig Config { get; set; } = new();

    /// <summary>
    /// Webhook secret for this project
    /// </summary>
    public string? WebhookSecret { get; set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Project-specific configuration
/// </summary>
public class ProjectConfig
{
    /// <summary>
    /// Auto-sync when new release is available
    /// </summary>
    public bool AutoSync { get; set; } = false;

    /// <summary>
    /// Backup configuration
    /// </summary>
    public ProjectBackupConfig? Backup { get; set; }

    /// <summary>
    /// Auto-rollback on error
    /// </summary>
    public bool AutoRollbackOnError { get; set; } = true;

    /// <summary>
    /// Rollback timeout in seconds
    /// </summary>
    public int RollbackTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Files/folders to exclude from sync
    /// </summary>
    public List<string>? ExcludePaths { get; set; } = [".env", ".env.*", "storage/", "node_modules/", ".git/"];

    /// <summary>
    /// Files/folders to include (if empty, include all)
    /// </summary>
    public List<string>? IncludePaths { get; set; } = [];

    /// <summary>
    /// Commands to run before sync
    /// </summary>
    public List<string>? PreSyncCommands { get; set; } = [];

    /// <summary>
    /// Commands to run after sync
    /// </summary>
    public List<string>? PostSyncCommands { get; set; } = [];

    /// <summary>
    /// Use framework default commands
    /// </summary>
    public bool UseDefaultCommands { get; set; } = true;

    /// <summary>
    /// File permission settings
    /// </summary>
    public FilePermissions? Permissions { get; set; }

    /// <summary>
    /// Health check configuration
    /// </summary>
    public HealthCheckConfig? HealthCheck { get; set; }

    /// <summary>
    /// Notification settings for this project
    /// </summary>
    public List<NotificationChannel> NotifyChannels { get; set; } = [];

    /// <summary>
    /// AI mode override for this project (null = use global)
    /// </summary>
    public AiMode? AiModeOverride { get; set; }
}

/// <summary>
/// Project backup configuration
/// </summary>
public class ProjectBackupConfig
{
    public bool Enabled { get; set; } = true;
    public string? Directory { get; set; }
    public int RetentionCount { get; set; } = 5;
}

/// <summary>
/// File permission settings
/// </summary>
public class FilePermissions
{
    public bool Enabled { get; set; } = false;
    public string DefaultFileMode { get; set; } = "644";
    public string DefaultDirMode { get; set; } = "755";
    public string? Owner { get; set; }
    public string? Group { get; set; }
    public Dictionary<string, string> CustomModes { get; set; } = new();
}

/// <summary>
/// Health check configuration
/// </summary>
public class HealthCheckConfig
{
    public bool Enabled { get; set; } = true;
    public string? Url { get; set; }
    public string Method { get; set; } = "GET";
    public int TimeoutSeconds { get; set; } = 30;
    public int ExpectedStatusCode { get; set; } = 200;
    public string? ExpectedContent { get; set; }
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
    public bool RollbackOnFail { get; set; } = true;
}
