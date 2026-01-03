namespace XcluadeAgent.Shared.DTOs;

/// <summary>
/// AI status with detailed provider information
/// </summary>
public class AiStatusDetailDto
{
    public bool IsAvailable { get; set; }
    public string ActiveProvider { get; set; } = string.Empty;
    public string? Model { get; set; }
    public bool HasGpu { get; set; }
    public string? Message { get; set; }
    public List<AiProviderStatusDto> Providers { get; set; } = [];
    public AiUsageDto? Usage { get; set; }
}

/// <summary>
/// AI provider status
/// </summary>
public class AiProviderStatusDto
{
    public string Provider { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
    public bool IsReachable { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// AI usage summary
/// </summary>
public class AiUsageDto
{
    public int TotalRequests { get; set; }
    public int TotalTokensUsed { get; set; }
    public decimal TotalCostUsd { get; set; }
    public decimal BudgetUsd { get; set; }
    public decimal BudgetRemainingUsd { get; set; }
    public int BudgetUsedPercent { get; set; }
}

/// <summary>
/// AI mode information
/// </summary>
public class AiModeInfoDto
{
    public string Mode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string RiskColor { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public bool RequiresApproval { get; set; }
    public bool CanModifyCode { get; set; }
}

/// <summary>
/// Request for AI analysis
/// </summary>
public class AiAnalyzeRequest
{
    public Guid ProjectId { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? LogContent { get; set; }
}

/// <summary>
/// AI analysis result
/// </summary>
public class AiAnalysisResultDto
{
    public bool Success { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? RootCause { get; set; }
    public string? Explanation { get; set; }
    public List<string> PossibleSolutions { get; set; } = [];
    public string? RecommendedAction { get; set; }
    public int TokensUsed { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// AI suggestion result
/// </summary>
public class AiSuggestionResultDto
{
    public bool Success { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<SuggestedChangeDto> Changes { get; set; } = [];
    public List<string> Commands { get; set; } = [];
    public string? AdditionalNotes { get; set; }
    public int TokensUsed { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Suggested code change
/// </summary>
public class SuggestedChangeDto
{
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BeforeSnippet { get; set; }
    public string? AfterSnippet { get; set; }
    public int? LineNumber { get; set; }
}

/// <summary>
/// AI patch result
/// </summary>
public class AiPatchResultDto
{
    public Guid Id { get; set; }
    public bool Success { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<FilePatchDto> Patches { get; set; } = [];
    public List<string> PreCommands { get; set; } = [];
    public List<string> PostCommands { get; set; } = [];
    public int TokensUsed { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// File patch
/// </summary>
public class FilePatchDto
{
    public string FilePath { get; set; } = string.Empty;
    public string? DiffContent { get; set; }
    public bool IsNewFile { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Apply patch result
/// </summary>
public class ApplyPatchResultDto
{
    public bool Success { get; set; }
    public bool AppliedToProduction { get; set; }
    public bool AppliedToSandbox { get; set; }
    public bool PendingApproval { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> AppliedFiles { get; set; } = [];
}

/// <summary>
/// AI usage statistics
/// </summary>
public class AiUsageStatsDto
{
    public int TotalRequests { get; set; }
    public int TotalTokensUsed { get; set; }
    public decimal TotalCostUsd { get; set; }
    public decimal BudgetUsd { get; set; }
    public decimal BudgetRemainingUsd { get; set; }
    public int BudgetUsedPercent { get; set; }
    public Dictionary<string, ProviderUsageDto> ByProvider { get; set; } = new();
    public List<DailyUsageDto> DailyUsage { get; set; } = [];
}

/// <summary>
/// Provider usage
/// </summary>
public class ProviderUsageDto
{
    public int Requests { get; set; }
    public int TokensUsed { get; set; }
    public decimal CostUsd { get; set; }
}

/// <summary>
/// Daily usage
/// </summary>
public class DailyUsageDto
{
    public DateTime Date { get; set; }
    public int Requests { get; set; }
    public int TokensUsed { get; set; }
    public decimal CostUsd { get; set; }
}

/// <summary>
/// Failed sync for AI analysis
/// </summary>
public class FailedSyncDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public string? Version { get; set; }
}
