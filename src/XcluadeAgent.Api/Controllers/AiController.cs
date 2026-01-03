using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Shared.DTOs;

namespace XcluadeAgent.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly IProjectRepository _projectRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly ILogger<AiController> _logger;

    public AiController(
        IAiService aiService,
        IProjectRepository projectRepository,
        ISyncHistoryRepository syncHistoryRepository,
        ILogger<AiController> logger)
    {
        _aiService = aiService;
        _projectRepository = projectRepository;
        _syncHistoryRepository = syncHistoryRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get AI availability and status
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<AiStatusDetailDto>>> GetStatus(CancellationToken cancellationToken)
    {
        var availability = await _aiService.CheckAvailabilityAsync(cancellationToken);
        var usage = await _aiService.GetUsageStatsAsync(
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            cancellationToken);

        var status = new AiStatusDetailDto
        {
            IsAvailable = availability.IsAvailable,
            ActiveProvider = availability.ActiveProvider.ToString(),
            Model = availability.ProviderModel,
            HasGpu = availability.HasGpu,
            Message = availability.Message,
            Providers = availability.Providers.Select(p => new AiProviderStatusDto
            {
                Provider = p.Key.ToString(),
                IsConfigured = p.Value.IsConfigured,
                IsReachable = p.Value.IsReachable,
                ErrorMessage = p.Value.ErrorMessage
            }).ToList(),
            Usage = new AiUsageDto
            {
                TotalRequests = usage.TotalRequests,
                TotalTokensUsed = usage.TotalTokensUsed,
                TotalCostUsd = usage.TotalCostUsd,
                BudgetUsd = usage.BudgetUsd,
                BudgetRemainingUsd = usage.BudgetRemainingUsd,
                BudgetUsedPercent = usage.BudgetUsedPercent
            }
        };

        return Ok(ApiResponse<AiStatusDetailDto>.Ok(status));
    }

    /// <summary>
    /// Get AI mode information
    /// </summary>
    [HttpGet("modes")]
    public ActionResult<ApiResponse<List<AiModeInfoDto>>> GetModes()
    {
        var modes = Enum.GetValues<AiMode>()
            .Select(mode => _aiService.GetModeInfo(mode))
            .Select(info => new AiModeInfoDto
            {
                Mode = info.Mode.ToString(),
                DisplayName = info.DisplayName,
                Description = info.Description,
                RiskLevel = info.RiskLevel,
                RiskColor = info.RiskColor,
                Capabilities = info.Capabilities,
                Warnings = info.Warnings,
                RequiresApproval = info.RequiresApproval,
                CanModifyCode = info.CanModifyCode
            })
            .ToList();

        return Ok(ApiResponse<List<AiModeInfoDto>>.Ok(modes));
    }

    /// <summary>
    /// Analyze an error with AI
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<ApiResponse<AiAnalysisResultDto>>> AnalyzeError(
        [FromBody] AiAnalyzeRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId);
        if (project == null)
        {
            return NotFound(ApiResponse<AiAnalysisResultDto>.Fail("Project not found"));
        }

        var context = new ErrorContext
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            Framework = project.Framework,
            ErrorMessage = request.ErrorMessage,
            StackTrace = request.StackTrace,
            LogContent = request.LogContent
        };

        _logger.LogInformation("AI analysis requested for project {Project}", project.Name);

        var analysis = await _aiService.AnalyzeErrorAsync(context, cancellationToken);

        var result = new AiAnalysisResultDto
        {
            Success = analysis.Success,
            Provider = analysis.Provider.ToString(),
            Summary = analysis.Summary,
            RootCause = analysis.RootCause,
            Explanation = analysis.Explanation,
            PossibleSolutions = analysis.PossibleSolutions,
            RecommendedAction = analysis.RecommendedAction,
            TokensUsed = analysis.TokensUsed,
            EstimatedCost = analysis.EstimatedCost,
            ErrorMessage = analysis.ErrorMessage
        };

        return Ok(ApiResponse<AiAnalysisResultDto>.Ok(result));
    }

    /// <summary>
    /// Get AI fix suggestion
    /// </summary>
    [HttpPost("suggest")]
    public async Task<ActionResult<ApiResponse<AiSuggestionResultDto>>> SuggestFix(
        [FromBody] AiAnalyzeRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId);
        if (project == null)
        {
            return NotFound(ApiResponse<AiSuggestionResultDto>.Fail("Project not found"));
        }

        var context = new ErrorContext
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            Framework = project.Framework,
            ErrorMessage = request.ErrorMessage,
            StackTrace = request.StackTrace,
            LogContent = request.LogContent
        };

        _logger.LogInformation("AI suggestion requested for project {Project}", project.Name);

        var suggestion = await _aiService.SuggestFixAsync(context, cancellationToken);

        var result = new AiSuggestionResultDto
        {
            Success = suggestion.Success,
            Provider = suggestion.Provider.ToString(),
            Description = suggestion.Description,
            Changes = suggestion.Changes.Select(c => new SuggestedChangeDto
            {
                FilePath = c.FilePath,
                Description = c.Description,
                BeforeSnippet = c.BeforeSnippet,
                AfterSnippet = c.AfterSnippet,
                LineNumber = c.LineNumber
            }).ToList(),
            Commands = suggestion.Commands,
            AdditionalNotes = suggestion.AdditionalNotes,
            TokensUsed = suggestion.TokensUsed,
            EstimatedCost = suggestion.EstimatedCost,
            ErrorMessage = suggestion.ErrorMessage
        };

        return Ok(ApiResponse<AiSuggestionResultDto>.Ok(result));
    }

    /// <summary>
    /// Generate AI code patch
    /// </summary>
    [HttpPost("patch")]
    public async Task<ActionResult<ApiResponse<AiPatchResultDto>>> GeneratePatch(
        [FromBody] AiAnalyzeRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId);
        if (project == null)
        {
            return NotFound(ApiResponse<AiPatchResultDto>.Fail("Project not found"));
        }

        var context = new ErrorContext
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            Framework = project.Framework,
            ErrorMessage = request.ErrorMessage,
            StackTrace = request.StackTrace,
            LogContent = request.LogContent
        };

        _logger.LogInformation("AI patch requested for project {Project}", project.Name);

        var patch = await _aiService.GeneratePatchAsync(context, cancellationToken);

        var result = new AiPatchResultDto
        {
            Id = patch.Id,
            Success = patch.Success,
            Provider = patch.Provider.ToString(),
            Description = patch.Description,
            Patches = patch.Patches.Select(p => new FilePatchDto
            {
                FilePath = p.FilePath,
                DiffContent = p.DiffContent,
                IsNewFile = p.IsNewFile,
                IsDeleted = p.IsDeleted
            }).ToList(),
            PreCommands = patch.PreCommands,
            PostCommands = patch.PostCommands,
            TokensUsed = patch.TokensUsed,
            EstimatedCost = patch.EstimatedCost,
            ErrorMessage = patch.ErrorMessage,
            CreatedAt = patch.CreatedAt,
            ExpiresAt = patch.ExpiresAt
        };

        return Ok(ApiResponse<AiPatchResultDto>.Ok(result));
    }

    /// <summary>
    /// Apply AI patch
    /// </summary>
    [HttpPost("apply/{patchId:guid}")]
    public async Task<ActionResult<ApiResponse<ApplyPatchResultDto>>> ApplyPatch(
        Guid patchId,
        [FromQuery] Guid projectId,
        [FromQuery] bool requireApproval = true,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            return NotFound(ApiResponse<ApplyPatchResultDto>.Fail("Project not found"));
        }

        // TODO: Get patch from storage by patchId
        _logger.LogInformation("AI patch {PatchId} apply requested for project {Project}", patchId, project.Name);

        return Ok(ApiResponse<ApplyPatchResultDto>.Fail("Patch application not yet implemented"));
    }

    /// <summary>
    /// Get AI usage statistics
    /// </summary>
    [HttpGet("usage")]
    public async Task<ActionResult<ApiResponse<AiUsageStatsDto>>> GetUsageStats(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var stats = await _aiService.GetUsageStatsAsync(
            from ?? DateTime.UtcNow.AddDays(-30),
            to ?? DateTime.UtcNow,
            cancellationToken);

        var result = new AiUsageStatsDto
        {
            TotalRequests = stats.TotalRequests,
            TotalTokensUsed = stats.TotalTokensUsed,
            TotalCostUsd = stats.TotalCostUsd,
            BudgetUsd = stats.BudgetUsd,
            BudgetRemainingUsd = stats.BudgetRemainingUsd,
            BudgetUsedPercent = stats.BudgetUsedPercent,
            ByProvider = stats.ByProvider.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => new ProviderUsageDto
                {
                    Requests = kvp.Value.Requests,
                    TokensUsed = kvp.Value.TokensUsed,
                    CostUsd = kvp.Value.CostUsd
                }),
            DailyUsage = stats.DailyUsage.Select(d => new DailyUsageDto
            {
                Date = d.Date,
                Requests = d.Requests,
                TokensUsed = d.TokensUsed,
                CostUsd = d.CostUsd
            }).ToList()
        };

        return Ok(ApiResponse<AiUsageStatsDto>.Ok(result));
    }

    /// <summary>
    /// Get recent failed syncs for AI analysis
    /// </summary>
    [HttpGet("failed-syncs")]
    public async Task<ActionResult<ApiResponse<List<FailedSyncDto>>>> GetFailedSyncs(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var history = await _syncHistoryRepository.GetRecentAsync(100);
        var failed = history
            .Where(h => !h.Success && !string.IsNullOrEmpty(h.ErrorMessage))
            .Take(limit)
            .Select(h => new FailedSyncDto
            {
                Id = h.Id,
                ProjectId = h.ProjectId,
                ProjectName = h.ProjectName,
                ErrorMessage = h.ErrorMessage ?? "Unknown error",
                StartedAt = h.StartedAt,
                Version = h.ToVersion
            })
            .ToList();

        return Ok(ApiResponse<List<FailedSyncDto>>.Ok(failed));
    }
}
