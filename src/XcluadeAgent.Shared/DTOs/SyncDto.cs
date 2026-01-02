using XcluadeAgent.Core.Enums;

namespace XcluadeAgent.Shared.DTOs;

/// <summary>
/// Sync request
/// </summary>
public class SyncRequest
{
    public bool DryRun { get; set; } = false;
    public string? TargetVersion { get; set; }
    public bool Force { get; set; } = false;
}

/// <summary>
/// Sync response
/// </summary>
public class SyncResultDto
{
    public bool Success { get; set; }
    public Guid? HistoryId { get; set; }
    public string? FromVersion { get; set; }
    public string? ToVersion { get; set; }
    public int FilesChanged { get; set; }
    public string BytesTransferred { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public bool RolledBack { get; set; }
    public List<CommandResultDto> Commands { get; set; } = [];
    public HealthCheckResultDto? HealthCheck { get; set; }
}

/// <summary>
/// Sync preview response
/// </summary>
public class SyncPreviewDto
{
    public string CurrentVersion { get; set; } = string.Empty;
    public string NewVersion { get; set; } = string.Empty;
    public List<FileChangeDto> Files { get; set; } = [];
    public List<string> Commands { get; set; } = [];
    public string TotalSize { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
}

/// <summary>
/// File change DTO
/// </summary>
public class FileChangeDto
{
    public string Path { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public string? OldSize { get; set; }
    public string? NewSize { get; set; }
}

/// <summary>
/// Command result DTO
/// </summary>
public class CommandResultDto
{
    public string Command { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public string Duration { get; set; } = string.Empty;
}

/// <summary>
/// Health check result DTO
/// </summary>
public class HealthCheckResultDto
{
    public bool Success { get; set; }
    public string Url { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string ResponseTime { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Sync history DTO
/// </summary>
public class SyncHistoryDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
    public string? InitiatedBy { get; set; }
    public string? FromVersion { get; set; }
    public string? ToVersion { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Duration { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int FilesChanged { get; set; }
    public string BytesTransferred { get; set; } = string.Empty;
    public bool IsRollback { get; set; }
    public bool HasBackup { get; set; }
    public AiAnalysisDto? AiAnalysis { get; set; }
}

/// <summary>
/// Rollback request
/// </summary>
public class RollbackRequest
{
    public Guid? HistoryId { get; set; }
    public bool ToLastBackup { get; set; } = false;
}

/// <summary>
/// Rollback option DTO
/// </summary>
public class RollbackOptionDto
{
    public Guid HistoryId { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; }
    public string SyncedAgo { get; set; } = string.Empty;
    public bool HasBackup { get; set; }
}

/// <summary>
/// AI analysis DTO
/// </summary>
public class AiAnalysisDto
{
    public string Provider { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? Analysis { get; set; }
    public string? SuggestedFix { get; set; }
    public bool FixApplied { get; set; }
    public bool ApprovedByUser { get; set; }
    public string? ApprovedBy { get; set; }
    public int TokensUsed { get; set; }
    public string EstimatedCost { get; set; } = string.Empty;
}
