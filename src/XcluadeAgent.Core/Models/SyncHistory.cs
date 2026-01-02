using XcluadeAgent.Core.Enums;

namespace XcluadeAgent.Core.Models;

/// <summary>
/// Represents a sync history entry
/// </summary>
public class SyncHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// What triggered this sync
    /// </summary>
    public SyncTrigger Trigger { get; set; }

    /// <summary>
    /// Who/what initiated the sync
    /// </summary>
    public string? InitiatedBy { get; set; }

    /// <summary>
    /// Version/release synced to (alias for ToVersion)
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Version before sync
    /// </summary>
    public string? FromVersion { get; set; }

    /// <summary>
    /// Version after sync
    /// </summary>
    public string? ToVersion { get; set; }

    /// <summary>
    /// Error message if sync failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Release information
    /// </summary>
    public string? ReleaseInfo { get; set; }

    /// <summary>
    /// Sync started at
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Sync completed at
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Was the sync successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Status message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Error details if failed
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Number of files synced
    /// </summary>
    public int FilesChanged { get; set; }

    /// <summary>
    /// Total size in bytes
    /// </summary>
    public long BytesTransferred { get; set; }

    /// <summary>
    /// Backup path if backup was created
    /// </summary>
    public string? BackupPath { get; set; }

    /// <summary>
    /// Was this a rollback operation
    /// </summary>
    public bool IsRollback { get; set; }

    /// <summary>
    /// Rollback source history id
    /// </summary>
    public Guid? RollbackFromId { get; set; }

    /// <summary>
    /// Post-deploy command outputs
    /// </summary>
    public List<CommandResult> CommandResults { get; set; } = [];

    /// <summary>
    /// Health check result
    /// </summary>
    public HealthCheckResult? HealthCheckResult { get; set; }

    /// <summary>
    /// AI analysis if enabled
    /// </summary>
    public object? AiAnalysis { get; set; }
}

/// <summary>
/// Result of a post-deploy command
/// </summary>
public class CommandResult
{
    public string Command { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public string? Output { get; set; }
    public string? ErrorOutput { get; set; }
    public long DurationMs { get; set; }
    public bool Success { get; set; }
}

/// <summary>
/// Health check result
/// </summary>
public class HealthCheckResult
{
    public bool Success { get; set; }
    public string? Url { get; set; }
    public int ExpectedStatusCode { get; set; }
    public int ActualStatusCode { get; set; }
    public long ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// AI analysis result
/// </summary>
public class AiAnalysis
{
    public AiProvider Provider { get; set; }
    public string? Model { get; set; }
    public string? Analysis { get; set; }
    public string? SuggestedFix { get; set; }
    public string? CodePatch { get; set; }
    public bool FixApplied { get; set; }
    public bool ApprovedByUser { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int TokensUsed { get; set; }
    public decimal EstimatedCost { get; set; }
}
