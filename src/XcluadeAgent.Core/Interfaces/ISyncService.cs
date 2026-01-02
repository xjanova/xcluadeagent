using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Core.Interfaces;

/// <summary>
/// Service for syncing projects with GitHub
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Sync a project from GitHub
    /// </summary>
    Task<SyncResult> SyncAsync(Guid projectId, SyncTrigger trigger, string? initiatedBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sync a project (dry run - don't actually apply changes)
    /// </summary>
    Task<SyncPreview> PreviewSyncAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback to a specific version
    /// </summary>
    Task<SyncResult> RollbackAsync(Guid projectId, Guid historyId, string? initiatedBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback to last backup
    /// </summary>
    Task<SyncResult> RollbackToLastBackupAsync(Guid projectId, string? initiatedBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available versions for rollback
    /// </summary>
    Task<IEnumerable<RollbackOption>> GetRollbackOptionsAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a project needs sync
    /// </summary>
    Task<bool> NeedsSyncAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get sync status
    /// </summary>
    Task<SyncStatus> GetStatusAsync(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a sync operation
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public Guid? HistoryId { get; set; }
    public string? Version { get; set; }
    public int FilesChanged { get; set; }
    public long BytesTransferred { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? BackupPath { get; set; }
    public List<CommandResult> CommandResults { get; set; } = [];
    public HealthCheckResult? HealthCheckResult { get; set; }
    public bool RolledBack { get; set; }
}

/// <summary>
/// Preview of what sync will do
/// </summary>
public class SyncPreview
{
    public string CurrentVersion { get; set; } = string.Empty;
    public string NewVersion { get; set; } = string.Empty;
    public List<FileChange> FileChanges { get; set; } = [];
    public List<string> CommandsToRun { get; set; } = [];
    public long TotalSizeBytes { get; set; }
}

/// <summary>
/// File change info
/// </summary>
public class FileChange
{
    public string Path { get; set; } = string.Empty;
    public FileChangeType ChangeType { get; set; }
    public long? OldSize { get; set; }
    public long? NewSize { get; set; }
}

public enum FileChangeType
{
    Added,
    Modified,
    Deleted
}

/// <summary>
/// Rollback option
/// </summary>
public class RollbackOption
{
    public Guid HistoryId { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; }
    public string? BackupPath { get; set; }
    public bool HasBackup { get; set; }
}

/// <summary>
/// Current sync status
/// </summary>
public class SyncStatus
{
    public ProjectStatus Status { get; set; }
    public string? CurrentVersion { get; set; }
    public string? LatestVersion { get; set; }
    public bool NeedsSync { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? LastError { get; set; }
}
