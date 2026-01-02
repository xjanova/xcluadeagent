using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Core.Interfaces;

/// <summary>
/// Service for AI-powered code analysis and fixes
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Check if AI is available and configured
    /// </summary>
    Task<AiAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze an error and provide insights
    /// </summary>
    Task<AiErrorAnalysis> AnalyzeErrorAsync(ErrorContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a fix suggestion for an error
    /// </summary>
    Task<AiFixSuggestion> SuggestFixAsync(ErrorContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a code patch to fix an issue
    /// </summary>
    Task<AiCodePatch> GeneratePatchAsync(ErrorContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply a code patch
    /// </summary>
    Task<ApplyPatchResult> ApplyPatchAsync(Guid projectId, AiCodePatch patch, bool requireApproval = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current AI usage/costs
    /// </summary>
    Task<AiUsageStats> GetUsageStatsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get AI mode description and warnings
    /// </summary>
    AiModeInfo GetModeInfo(AiMode mode);
}

/// <summary>
/// AI availability status
/// </summary>
public class AiAvailability
{
    public bool IsAvailable { get; set; }
    public AiProvider ActiveProvider { get; set; }
    public string? ProviderModel { get; set; }
    public bool HasGpu { get; set; }
    public string? Message { get; set; }
    public Dictionary<AiProvider, ProviderStatus> Providers { get; set; } = new();
}

/// <summary>
/// Provider status
/// </summary>
public class ProviderStatus
{
    public bool IsConfigured { get; set; }
    public bool IsReachable { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Error context for AI analysis
/// </summary>
public class ErrorContext
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public FrameworkType Framework { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? LogContent { get; set; }
    public List<string> RelatedFiles { get; set; } = [];
    public Dictionary<string, string> FileContents { get; set; } = new();
    public string? RecentChanges { get; set; }
}

/// <summary>
/// AI error analysis result
/// </summary>
public class AiErrorAnalysis
{
    public bool Success { get; set; }
    public AiProvider Provider { get; set; }
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
/// AI fix suggestion
/// </summary>
public class AiFixSuggestion
{
    public bool Success { get; set; }
    public AiProvider Provider { get; set; }
    public string? Description { get; set; }
    public List<SuggestedChange> Changes { get; set; } = [];
    public List<string> Commands { get; set; } = [];
    public string? AdditionalNotes { get; set; }
    public int TokensUsed { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Suggested code change
/// </summary>
public class SuggestedChange
{
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BeforeSnippet { get; set; }
    public string? AfterSnippet { get; set; }
    public int? LineNumber { get; set; }
}

/// <summary>
/// AI-generated code patch
/// </summary>
public class AiCodePatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Success { get; set; }
    public AiProvider Provider { get; set; }
    public string? Description { get; set; }
    public List<FilePatch> Patches { get; set; } = [];
    public List<string> PreCommands { get; set; } = [];
    public List<string> PostCommands { get; set; } = [];
    public int TokensUsed { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// File patch
/// </summary>
public class FilePatch
{
    public string FilePath { get; set; } = string.Empty;
    public string? OriginalContent { get; set; }
    public string? NewContent { get; set; }
    public string? DiffContent { get; set; }
    public bool IsNewFile { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Result of applying a patch
/// </summary>
public class ApplyPatchResult
{
    public bool Success { get; set; }
    public bool AppliedToProduction { get; set; }
    public bool AppliedToSandbox { get; set; }
    public bool PendingApproval { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> AppliedFiles { get; set; } = [];
    public HealthCheckResult? HealthCheckResult { get; set; }
}

/// <summary>
/// AI usage statistics
/// </summary>
public class AiUsageStats
{
    public int TotalRequests { get; set; }
    public int TotalTokensUsed { get; set; }
    public decimal TotalCostUsd { get; set; }
    public decimal BudgetUsd { get; set; }
    public decimal BudgetRemainingUsd { get; set; }
    public int BudgetUsedPercent { get; set; }
    public Dictionary<AiProvider, ProviderUsage> ByProvider { get; set; } = new();
    public List<DailyUsage> DailyUsage { get; set; } = [];
}

/// <summary>
/// Usage by provider
/// </summary>
public class ProviderUsage
{
    public int Requests { get; set; }
    public int TokensUsed { get; set; }
    public decimal CostUsd { get; set; }
}

/// <summary>
/// Daily usage
/// </summary>
public class DailyUsage
{
    public DateTime Date { get; set; }
    public int Requests { get; set; }
    public int TokensUsed { get; set; }
    public decimal CostUsd { get; set; }
}

/// <summary>
/// AI mode information
/// </summary>
public class AiModeInfo
{
    public AiMode Mode { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string RiskColor { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public bool RequiresApproval { get; set; }
    public bool CanModifyCode { get; set; }
}
