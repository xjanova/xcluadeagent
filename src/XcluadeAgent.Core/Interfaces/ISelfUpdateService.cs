namespace XcluadeAgent.Core.Interfaces;

/// <summary>
/// Service for self-updating XcluadeAgent
/// </summary>
public interface ISelfUpdateService
{
    /// <summary>
    /// Check if an update is available
    /// </summary>
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Download and apply update
    /// </summary>
    Task<UpdateResult> ApplyUpdateAsync(string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current version
    /// </summary>
    string GetCurrentVersion();

    /// <summary>
    /// Get update history
    /// </summary>
    Task<List<UpdateHistoryEntry>> GetUpdateHistoryAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of checking for updates
/// </summary>
public class UpdateCheckResult
{
    public bool UpdateAvailable { get; set; }
    public string CurrentVersion { get; set; } = string.Empty;
    public string? LatestVersion { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? DownloadUrl { get; set; }
    public DateTime? PublishedAt { get; set; }
    public long? DownloadSize { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ReleaseAsset> Assets { get; set; } = new();
}

/// <summary>
/// Release asset information
/// </summary>
public class ReleaseAsset
{
    public string Name { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public long Size { get; set; }
    public string ContentType { get; set; } = string.Empty;
}

/// <summary>
/// Result of applying an update
/// </summary>
public class UpdateResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? InstalledVersion { get; set; }
    public bool RestartRequired { get; set; }
    public string? BackupPath { get; set; }

    public static UpdateResult Succeeded(string version, string? backupPath = null) => new()
    {
        Success = true,
        InstalledVersion = version,
        RestartRequired = true,
        BackupPath = backupPath
    };

    public static UpdateResult Failed(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };
}

/// <summary>
/// Update history entry
/// </summary>
public class UpdateHistoryEntry
{
    public string FromVersion { get; set; } = string.Empty;
    public string ToVersion { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? BackupPath { get; set; }
}
