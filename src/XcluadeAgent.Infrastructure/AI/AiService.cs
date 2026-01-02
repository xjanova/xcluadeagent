using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.AI;

/// <summary>
/// AI service implementation supporting multiple providers
/// </summary>
public class AiService : IAiService
{
    private readonly AiConfig _config;
    private readonly ILogger<AiService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public AiService(
        IOptions<AiConfig> config,
        ILogger<AiService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AiAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var availability = new AiAvailability
        {
            IsAvailable = false,
            Providers = new Dictionary<AiProvider, ProviderStatus>()
        };

        if (!_config.Enabled)
        {
            availability.Message = "AI is disabled in configuration";
            return availability;
        }

        // Check primary provider
        if (_config.Primary != null)
        {
            var primaryStatus = await CheckProviderAsync(_config.Primary, cancellationToken);
            availability.Providers[_config.Primary.Type] = primaryStatus;

            if (primaryStatus.IsReachable)
            {
                availability.IsAvailable = true;
                availability.ActiveProvider = _config.Primary.Type;
                availability.ProviderModel = _config.Primary.Model;
            }
        }

        // Check fallback provider
        if (_config.Fallback != null)
        {
            var fallbackStatus = await CheckProviderAsync(_config.Fallback, cancellationToken);
            availability.Providers[_config.Fallback.Type] = fallbackStatus;

            if (!availability.IsAvailable && fallbackStatus.IsReachable)
            {
                availability.IsAvailable = true;
                availability.ActiveProvider = _config.Fallback.Type;
                availability.ProviderModel = _config.Fallback.Model;
            }
        }

        // Check for GPU (Ollama)
        if (availability.ActiveProvider == AiProvider.Ollama)
        {
            availability.HasGpu = await CheckOllamaGpuAsync(cancellationToken);
        }

        availability.Message = availability.IsAvailable
            ? $"AI available via {availability.ActiveProvider} ({availability.ProviderModel})"
            : "No AI providers available";

        return availability;
    }

    public async Task<AiErrorAnalysis> AnalyzeErrorAsync(ErrorContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildAnalysisPrompt(context);
            var response = await SendPromptAsync(prompt, cancellationToken);
            var parsed = ParseAnalysisResponse(response.Content);
            var cost = CalculateCost(response.Provider, response.TokensUsed);

            TrackUsage(response.Provider, response.TokensUsed, cost);

            return new AiErrorAnalysis
            {
                Success = true,
                Provider = response.Provider,
                Summary = parsed.Summary,
                RootCause = parsed.RootCause,
                Explanation = parsed.Explanation,
                PossibleSolutions = parsed.Solutions,
                RecommendedAction = parsed.RecommendedAction,
                TokensUsed = response.TokensUsed,
                EstimatedCost = cost
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI error analysis failed");
            return new AiErrorAnalysis
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static ParsedAnalysis ParseAnalysisResponse(string content)
    {
        var result = new ParsedAnalysis();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var currentSection = "";
        var solutionsList = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.StartsWith("1.") || trimmedLine.Contains("Summary", StringComparison.OrdinalIgnoreCase))
            {
                currentSection = "summary";
                var value = ExtractValue(trimmedLine);
                if (!string.IsNullOrEmpty(value)) result.Summary = value;
            }
            else if (trimmedLine.StartsWith("2.") || trimmedLine.Contains("Root Cause", StringComparison.OrdinalIgnoreCase))
            {
                currentSection = "rootcause";
                var value = ExtractValue(trimmedLine);
                if (!string.IsNullOrEmpty(value)) result.RootCause = value;
            }
            else if (trimmedLine.StartsWith("3.") || trimmedLine.Contains("Explanation", StringComparison.OrdinalIgnoreCase))
            {
                currentSection = "explanation";
                var value = ExtractValue(trimmedLine);
                if (!string.IsNullOrEmpty(value)) result.Explanation = value;
            }
            else if (trimmedLine.StartsWith("4.") || trimmedLine.Contains("Solution", StringComparison.OrdinalIgnoreCase))
            {
                currentSection = "solutions";
            }
            else if (trimmedLine.StartsWith("5.") || trimmedLine.Contains("Recommend", StringComparison.OrdinalIgnoreCase))
            {
                currentSection = "recommended";
                var value = ExtractValue(trimmedLine);
                if (!string.IsNullOrEmpty(value)) result.RecommendedAction = value;
            }
            else if (currentSection == "solutions" && (trimmedLine.StartsWith("-") || trimmedLine.StartsWith("•")))
            {
                solutionsList.Add(trimmedLine.TrimStart('-', '•', ' '));
            }
            else if (!string.IsNullOrEmpty(currentSection) && !string.IsNullOrWhiteSpace(trimmedLine))
            {
                switch (currentSection)
                {
                    case "summary" when string.IsNullOrEmpty(result.Summary):
                        result.Summary = trimmedLine;
                        break;
                    case "rootcause" when string.IsNullOrEmpty(result.RootCause):
                        result.RootCause = trimmedLine;
                        break;
                    case "explanation" when string.IsNullOrEmpty(result.Explanation):
                        result.Explanation = trimmedLine;
                        break;
                    case "recommended" when string.IsNullOrEmpty(result.RecommendedAction):
                        result.RecommendedAction = trimmedLine;
                        break;
                }
            }
        }

        result.Solutions = solutionsList.Count > 0 ? solutionsList : ["Review the error details and logs for more context"];

        // Fallback values
        result.Summary ??= content.Length > 200 ? content[..200] + "..." : content;
        result.RootCause ??= "Unable to determine specific root cause";
        result.Explanation ??= "See the full AI response for details";
        result.RecommendedAction ??= "Review the suggested solutions";

        return result;
    }

    private static string ExtractValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        return colonIndex > 0 ? line[(colonIndex + 1)..].Trim() : "";
    }

    private class ParsedAnalysis
    {
        public string? Summary { get; set; }
        public string? RootCause { get; set; }
        public string? Explanation { get; set; }
        public List<string> Solutions { get; set; } = [];
        public string? RecommendedAction { get; set; }
    }

    public async Task<AiFixSuggestion> SuggestFixAsync(ErrorContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildFixSuggestionPrompt(context);
            var response = await SendPromptAsync(prompt, cancellationToken);

            return new AiFixSuggestion
            {
                Success = true,
                Provider = response.Provider,
                Description = response.FixDescription,
                Changes = response.SuggestedChanges,
                Commands = response.Commands,
                AdditionalNotes = response.Notes,
                TokensUsed = response.TokensUsed,
                EstimatedCost = CalculateCost(response.Provider, response.TokensUsed)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI fix suggestion failed");
            return new AiFixSuggestion
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<AiCodePatch> GeneratePatchAsync(ErrorContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildPatchGenerationPrompt(context);
            var response = await SendPromptAsync(prompt, cancellationToken);

            return new AiCodePatch
            {
                Success = true,
                Provider = response.Provider,
                Description = response.PatchDescription,
                Patches = response.FilePatches,
                PreCommands = response.PreCommands,
                PostCommands = response.PostCommands,
                TokensUsed = response.TokensUsed,
                EstimatedCost = CalculateCost(response.Provider, response.TokensUsed),
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI patch generation failed");
            return new AiCodePatch
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ApplyPatchResult> ApplyPatchAsync(Guid projectId, AiCodePatch patch, bool requireApproval = true, CancellationToken cancellationToken = default)
    {
        if (requireApproval && _config.Safety.RequireApproval)
        {
            _logger.LogInformation("Patch requires approval, creating approval request");
            return new ApplyPatchResult
            {
                Success = true,
                PendingApproval = true,
                ApprovalRequestId = Guid.NewGuid()
            };
        }

        var result = new ApplyPatchResult();
        var appliedFiles = new List<string>();

        try
        {
            // Run pre-commands
            foreach (var command in patch.PreCommands)
            {
                _logger.LogDebug("Running pre-command: {Command}", command);
                await RunCommandAsync(command, cancellationToken);
            }

            // Apply file patches
            foreach (var filePatch in patch.Patches)
            {
                try
                {
                    if (filePatch.IsDeleted && File.Exists(filePatch.FilePath))
                    {
                        File.Delete(filePatch.FilePath);
                        _logger.LogInformation("Deleted file: {Path}", filePatch.FilePath);
                    }
                    else if (!string.IsNullOrEmpty(filePatch.NewContent))
                    {
                        var directory = Path.GetDirectoryName(filePatch.FilePath);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        await File.WriteAllTextAsync(filePatch.FilePath, filePatch.NewContent, cancellationToken);
                        _logger.LogInformation("Applied patch to: {Path}", filePatch.FilePath);
                    }

                    appliedFiles.Add(filePatch.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply patch to {Path}", filePatch.FilePath);
                    throw;
                }
            }

            // Run post-commands
            foreach (var command in patch.PostCommands)
            {
                _logger.LogDebug("Running post-command: {Command}", command);
                await RunCommandAsync(command, cancellationToken);
            }

            result.Success = true;
            result.AppliedFiles = appliedFiles;
            result.AppliedToProduction = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply patch");
            result.Success = false;
            result.ErrorMessage = ex.Message;

            // Attempt to rollback applied changes
            foreach (var filePatch in patch.Patches.Where(p => appliedFiles.Contains(p.FilePath)))
            {
                try
                {
                    if (!string.IsNullOrEmpty(filePatch.OriginalContent))
                    {
                        await File.WriteAllTextAsync(filePatch.FilePath, filePatch.OriginalContent, cancellationToken);
                    }
                }
                catch
                {
                    // Best effort rollback
                }
            }
        }

        return result;
    }

    private async Task RunCommandAsync(string command, CancellationToken cancellationToken)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"Command failed: {error}");
        }
    }

    public async Task<AiUsageStats> GetUsageStatsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        // For now, read from in-memory tracking; in production, this would come from database
        var stats = new AiUsageStats
        {
            BudgetUsd = _config.CostControl?.MonthlyBudgetUsd ?? 100m,
            TotalRequests = _usageTracking.TotalRequests,
            TotalTokensUsed = _usageTracking.TotalTokens,
            TotalCostUsd = _usageTracking.TotalCost
        };

        stats.BudgetRemainingUsd = Math.Max(0, stats.BudgetUsd - stats.TotalCostUsd);
        stats.BudgetUsedPercent = stats.BudgetUsd > 0
            ? (int)((stats.TotalCostUsd / stats.BudgetUsd) * 100)
            : 0;

        stats.ByProvider = _usageTracking.ByProvider.ToDictionary(
            kvp => kvp.Key,
            kvp => new ProviderUsage
            {
                Requests = kvp.Value.Requests,
                TokensUsed = kvp.Value.Tokens,
                CostUsd = kvp.Value.Cost
            });

        return await Task.FromResult(stats);
    }

    private void TrackUsage(AiProvider provider, int tokens, decimal cost)
    {
        _usageTracking.TotalRequests++;
        _usageTracking.TotalTokens += tokens;
        _usageTracking.TotalCost += cost;

        if (!_usageTracking.ByProvider.TryGetValue(provider, out var providerStats))
        {
            providerStats = new UsageData();
            _usageTracking.ByProvider[provider] = providerStats;
        }

        providerStats.Requests++;
        providerStats.Tokens += tokens;
        providerStats.Cost += cost;
    }

    private static readonly UsageTracking _usageTracking = new();

    private class UsageTracking
    {
        public int TotalRequests { get; set; }
        public int TotalTokens { get; set; }
        public decimal TotalCost { get; set; }
        public Dictionary<AiProvider, UsageData> ByProvider { get; } = new();
    }

    private class UsageData
    {
        public int Requests { get; set; }
        public int Tokens { get; set; }
        public decimal Cost { get; set; }
    }

    public AiModeInfo GetModeInfo(AiMode mode)
    {
        var capabilities = GetModeCapabilities(mode);
        var warnings = GetModeWarnings(mode);

        return new AiModeInfo
        {
            Mode = mode,
            DisplayName = mode.GetDisplayName(),
            Description = mode.GetDescription(),
            RiskLevel = mode.GetRiskLevel(),
            RiskColor = mode.GetRiskColor(),
            Capabilities = capabilities,
            Warnings = warnings,
            RequiresApproval = mode.RequiresApproval(),
            CanModifyCode = mode.CanModifyCode()
        };
    }

    private async Task<ProviderStatus> CheckProviderAsync(AiProviderConfig config, CancellationToken cancellationToken)
    {
        var status = new ProviderStatus
        {
            IsConfigured = !string.IsNullOrEmpty(config.Endpoint) || !string.IsNullOrEmpty(config.ApiKey)
        };

        if (!status.IsConfigured)
        {
            status.ErrorMessage = "Provider not configured";
            return status;
        }

        try
        {
            switch (config.Type)
            {
                case AiProvider.Ollama:
                    status.IsReachable = await CheckOllamaAsync(config.Endpoint!, cancellationToken);
                    break;
                case AiProvider.Claude:
                    status.IsReachable = await CheckClaudeAsync(config.ApiKey!, cancellationToken);
                    break;
                case AiProvider.OpenAi:
                    status.IsReachable = await CheckOpenAiAsync(config.ApiKey!, cancellationToken);
                    break;
                default:
                    status.ErrorMessage = "Unknown provider type";
                    break;
            }
        }
        catch (Exception ex)
        {
            status.ErrorMessage = ex.Message;
        }

        return status;
    }

    private async Task<bool> CheckOllamaAsync(string endpoint, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            var response = await client.GetAsync($"{endpoint.TrimEnd('/')}/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckOllamaGpuAsync(CancellationToken cancellationToken)
    {
        // Try to detect GPU by checking Ollama's response
        // This is a simplified check - actual GPU detection would be more complex
        return await Task.FromResult(false);
    }

    private async Task<bool> CheckClaudeAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        try
        {
            // Simple validation - just check if the key format is valid
            return !string.IsNullOrEmpty(apiKey) && apiKey.StartsWith("sk-ant-");
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckOpenAiAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        try
        {
            var response = await client.GetAsync("https://api.openai.com/v1/models", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string BuildAnalysisPrompt(ErrorContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the following error and provide insights:");
        sb.AppendLine();
        sb.AppendLine($"Project: {context.ProjectName}");
        sb.AppendLine($"Framework: {context.Framework}");
        sb.AppendLine();
        sb.AppendLine("Error Message:");
        sb.AppendLine(context.ErrorMessage);

        if (!string.IsNullOrEmpty(context.StackTrace))
        {
            sb.AppendLine();
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(context.StackTrace);
        }

        if (!string.IsNullOrEmpty(context.LogContent))
        {
            sb.AppendLine();
            sb.AppendLine("Log Content:");
            sb.AppendLine(context.LogContent);
        }

        sb.AppendLine();
        sb.AppendLine("Please provide:");
        sb.AppendLine("1. A brief summary of the error");
        sb.AppendLine("2. The likely root cause");
        sb.AppendLine("3. An explanation in simple terms");
        sb.AppendLine("4. Possible solutions (list)");
        sb.AppendLine("5. Recommended action");

        return sb.ToString();
    }

    private string BuildFixSuggestionPrompt(ErrorContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Suggest a fix for the following error:");
        sb.AppendLine();
        sb.AppendLine($"Project: {context.ProjectName}");
        sb.AppendLine($"Framework: {context.Framework}");
        sb.AppendLine();
        sb.AppendLine("Error:");
        sb.AppendLine(context.ErrorMessage);

        foreach (var file in context.FileContents)
        {
            sb.AppendLine();
            sb.AppendLine($"File: {file.Key}");
            sb.AppendLine("```");
            sb.AppendLine(file.Value);
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("Provide specific code changes to fix this error.");

        return sb.ToString();
    }

    private string BuildPatchGenerationPrompt(ErrorContext context)
    {
        return BuildFixSuggestionPrompt(context) + "\n\nGenerate a complete patch with before/after code.";
    }

    private async Task<AiResponse> SendPromptAsync(string prompt, CancellationToken cancellationToken)
    {
        var provider = _config.Primary ?? throw new InvalidOperationException("No AI provider configured");

        return provider.Type switch
        {
            AiProvider.Ollama => await SendToOllamaAsync(provider, prompt, cancellationToken),
            AiProvider.Claude => await SendToClaudeAsync(provider, prompt, cancellationToken),
            AiProvider.OpenAi => await SendToOpenAiAsync(provider, prompt, cancellationToken),
            _ => throw new NotSupportedException($"Provider {provider.Type} not supported")
        };
    }

    private async Task<AiResponse> SendToOllamaAsync(AiProviderConfig config, string prompt, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);

        var request = new
        {
            model = config.Model ?? "qwen2.5-coder:7b",
            prompt = prompt,
            stream = false
        };

        var response = await client.PostAsJsonAsync($"{config.Endpoint}/api/generate", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: cancellationToken);

        return new AiResponse
        {
            Provider = AiProvider.Ollama,
            Content = result?.Response ?? "",
            TokensUsed = result?.EvalCount ?? 0
        };
    }

    private async Task<AiResponse> SendToClaudeAsync(AiProviderConfig config, string prompt, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
        client.DefaultRequestHeaders.Add("x-api-key", config.ApiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var request = new
        {
            model = config.Model ?? "claude-3-5-sonnet-20241022",
            max_tokens = config.MaxTokens,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var response = await client.PostAsJsonAsync("https://api.anthropic.com/v1/messages", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ClaudeResponse>(cancellationToken: cancellationToken);

        return new AiResponse
        {
            Provider = AiProvider.Claude,
            Content = result?.Content?.FirstOrDefault()?.Text ?? "",
            TokensUsed = (result?.Usage?.InputTokens ?? 0) + (result?.Usage?.OutputTokens ?? 0)
        };
    }

    private async Task<AiResponse> SendToOpenAiAsync(AiProviderConfig config, string prompt, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

        var request = new
        {
            model = config.Model ?? "gpt-4o",
            max_tokens = config.MaxTokens,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var response = await client.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAiResponse>(cancellationToken: cancellationToken);

        return new AiResponse
        {
            Provider = AiProvider.OpenAi,
            Content = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "",
            TokensUsed = result?.Usage?.TotalTokens ?? 0
        };
    }

    private decimal CalculateCost(AiProvider provider, int tokens)
    {
        // Approximate costs per 1M tokens
        return provider switch
        {
            AiProvider.Ollama => 0, // Free (local)
            AiProvider.Claude => tokens * 0.000015m, // ~$15/1M output tokens
            AiProvider.OpenAi => tokens * 0.00001m, // ~$10/1M output tokens
            _ => 0
        };
    }

    private List<string> GetModeCapabilities(AiMode mode) => mode switch
    {
        AiMode.Off => [],
        AiMode.Alert => ["Error analysis", "Smart categorization", "Intelligent notifications"],
        AiMode.Suggest => ["Error analysis", "Fix suggestions", "Code examples", "Documentation links"],
        AiMode.Review => ["Error analysis", "Fix suggestions", "Patch generation", "Diff preview", "Approval workflow"],
        AiMode.Sandbox => ["Error analysis", "Fix suggestions", "Patch generation", "Staging deployment", "Automated testing"],
        AiMode.Auto => ["Error analysis", "Fix suggestions", "Patch generation", "Production deployment", "Auto-rollback"],
        _ => []
    };

    private List<string> GetModeWarnings(AiMode mode) => mode switch
    {
        AiMode.Off => [],
        AiMode.Alert => [],
        AiMode.Suggest => ["AI may occasionally suggest incorrect fixes"],
        AiMode.Review => ["Review all changes carefully before approving"],
        AiMode.Sandbox => ["Ensure staging environment matches production", "Test thoroughly before promoting"],
        AiMode.Auto => [
            "⚠️ HIGH RISK: AI will modify production code",
            "Ensure auto-rollback is properly configured",
            "Monitor closely after enabling",
            "Not recommended for critical systems"
        ],
        _ => []
    };

    // Response DTOs
    private class AiResponse
    {
        public AiProvider Provider { get; set; }
        public string Content { get; set; } = "";
        public int TokensUsed { get; set; }

        // Parsed fields (would need proper parsing logic)
        public string? Summary { get; set; }
        public string? RootCause { get; set; }
        public string? Explanation { get; set; }
        public List<string> Solutions { get; set; } = [];
        public string? RecommendedAction { get; set; }
        public string? FixDescription { get; set; }
        public List<SuggestedChange> SuggestedChanges { get; set; } = [];
        public List<string> Commands { get; set; } = [];
        public string? Notes { get; set; }
        public string? PatchDescription { get; set; }
        public List<FilePatch> FilePatches { get; set; } = [];
        public List<string> PreCommands { get; set; } = [];
        public List<string> PostCommands { get; set; } = [];
    }

    private class OllamaResponse
    {
        public string? Response { get; set; }
        public int EvalCount { get; set; }
    }

    private class ClaudeResponse
    {
        public List<ClaudeContent>? Content { get; set; }
        public ClaudeUsage? Usage { get; set; }
    }

    private class ClaudeContent
    {
        public string? Text { get; set; }
    }

    private class ClaudeUsage
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }

    private class OpenAiResponse
    {
        public List<OpenAiChoice>? Choices { get; set; }
        public OpenAiUsage? Usage { get; set; }
    }

    private class OpenAiChoice
    {
        public OpenAiMessage? Message { get; set; }
    }

    private class OpenAiMessage
    {
        public string? Content { get; set; }
    }

    private class OpenAiUsage
    {
        public int TotalTokens { get; set; }
    }
}
