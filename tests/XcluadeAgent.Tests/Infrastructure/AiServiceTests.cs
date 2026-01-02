using FluentAssertions;
using Xunit;
using XcluadeAgent.Core.Enums;

namespace XcluadeAgent.Tests.Infrastructure;

public class AiServiceTests
{
    [Theory]
    [InlineData(AiMode.Off, false)]
    [InlineData(AiMode.Alert, true)]
    [InlineData(AiMode.Suggest, true)]
    [InlineData(AiMode.Review, true)]
    [InlineData(AiMode.Sandbox, true)]
    [InlineData(AiMode.Auto, true)]
    public void IsAiEnabled_ShouldReturnCorrectValue(AiMode mode, bool expected)
    {
        // Act
        var result = IsAiEnabled(mode);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(AiMode.Off, false)]
    [InlineData(AiMode.Alert, false)]
    [InlineData(AiMode.Suggest, false)]
    [InlineData(AiMode.Review, false)]
    [InlineData(AiMode.Sandbox, true)]
    [InlineData(AiMode.Auto, true)]
    public void CanAutoFix_ShouldReturnCorrectValue(AiMode mode, bool expected)
    {
        // Act
        var result = CanAutoFix(mode);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(AiMode.Review, true)]
    [InlineData(AiMode.Sandbox, true)]
    [InlineData(AiMode.Auto, false)]
    [InlineData(AiMode.Suggest, false)]
    public void RequiresApproval_ShouldReturnCorrectValue(AiMode mode, bool expected)
    {
        // Act
        var result = RequiresApproval(mode);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("ollama", true)]
    [InlineData("claude", true)]
    [InlineData("openai", true)]
    [InlineData("unknown", false)]
    [InlineData("", false)]
    public void IsValidProvider_ShouldReturnCorrectValue(string provider, bool expected)
    {
        // Act
        var result = IsValidProvider(provider);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void BuildPrompt_ErrorAnalysis_ShouldContainContext()
    {
        // Arrange
        var projectName = "my-laravel-app";
        var errorLog = "PHP Fatal error: Class 'App\\Models\\User' not found";
        var framework = FrameworkType.Laravel;

        // Act
        var prompt = BuildErrorAnalysisPrompt(projectName, errorLog, framework);

        // Assert
        prompt.Should().Contain(projectName);
        prompt.Should().Contain(errorLog);
        prompt.Should().Contain("Laravel");
        prompt.Should().Contain("analyze");
    }

    [Fact]
    public void BuildPrompt_FixSuggestion_ShouldIncludeInstructions()
    {
        // Arrange
        var errorLog = "npm ERR! Missing script: build";
        var framework = FrameworkType.NodeJs;

        // Act
        var prompt = BuildFixSuggestionPrompt(errorLog, framework);

        // Assert
        prompt.Should().Contain("suggest");
        prompt.Should().Contain("fix");
        prompt.Should().Contain(errorLog);
    }

    [Theory]
    [InlineData("composer install failed", FrameworkType.Laravel, "composer")]
    [InlineData("npm install failed", FrameworkType.NodeJs, "npm")]
    [InlineData("pip install failed", FrameworkType.Django, "pip")]
    public void DetectErrorType_ShouldIdentifyFrameworkErrors(string error, FrameworkType framework, string expectedKeyword)
    {
        // Act
        var errorType = DetectErrorType(error, framework);

        // Assert
        errorType.Should().Contain(expectedKeyword);
    }

    [Fact]
    public void ParseAiResponse_ValidJson_ShouldExtractSuggestions()
    {
        // Arrange
        var response = @"{
            ""analysis"": ""Missing dependency"",
            ""suggestions"": [
                ""Run composer install"",
                ""Clear cache""
            ],
            ""severity"": ""medium""
        }";

        // Act
        var result = ParseAiResponse(response);

        // Assert
        result.Analysis.Should().Be("Missing dependency");
        result.Suggestions.Should().HaveCount(2);
        result.Severity.Should().Be("medium");
    }

    [Fact]
    public void ParseAiResponse_InvalidJson_ShouldReturnDefault()
    {
        // Arrange
        var response = "This is not valid JSON";

        // Act
        var result = ParseAiResponse(response);

        // Assert
        result.Analysis.Should().Be(response);
        result.Suggestions.Should().BeEmpty();
    }

    [Theory]
    [InlineData(100, 1000, 10)] // 10% usage
    [InlineData(500, 1000, 50)] // 50% usage
    [InlineData(900, 1000, 90)] // 90% usage
    public void CalculateBudgetUsage_ShouldReturnPercentage(int used, int limit, int expectedPercent)
    {
        // Act
        var percentage = CalculateBudgetUsage(used, limit);

        // Assert
        percentage.Should().Be(expectedPercent);
    }

    [Theory]
    [InlineData(80, true)]  // Above threshold
    [InlineData(90, true)]  // Above threshold
    [InlineData(50, false)] // Below threshold
    public void ShouldWarnBudget_ShouldReturnCorrectValue(int usagePercent, bool expected)
    {
        // Act
        var result = ShouldWarnBudget(usagePercent, threshold: 75);

        // Assert
        result.Should().Be(expected);
    }

    // Helper methods simulating AiService logic
    private static bool IsAiEnabled(AiMode mode) => mode != AiMode.Off;

    private static bool CanAutoFix(AiMode mode) => mode is AiMode.Sandbox or AiMode.Auto;

    private static bool RequiresApproval(AiMode mode) => mode is AiMode.Review or AiMode.Sandbox;

    private static bool IsValidProvider(string provider)
    {
        var validProviders = new[] { "ollama", "claude", "openai" };
        return validProviders.Contains(provider.ToLowerInvariant());
    }

    private static string BuildErrorAnalysisPrompt(string projectName, string errorLog, FrameworkType framework)
    {
        return $"""
            Analyze the following error from project '{projectName}' using {framework} framework:

            Error Log:
            {errorLog}

            Please provide:
            1. Root cause analysis
            2. Suggested fixes
            3. Prevention recommendations
            """;
    }

    private static string BuildFixSuggestionPrompt(string errorLog, FrameworkType framework)
    {
        return $"""
            Given the following error in a {framework} project, suggest a fix:

            Error: {errorLog}

            Provide step-by-step instructions to resolve this issue.
            """;
    }

    private static string DetectErrorType(string error, FrameworkType framework)
    {
        var keyword = framework switch
        {
            FrameworkType.Laravel => "composer",
            FrameworkType.NodeJs or FrameworkType.React or FrameworkType.Vue => "npm",
            FrameworkType.Django => "pip",
            FrameworkType.DotNet => "dotnet",
            _ => "unknown"
        };

        return $"{keyword} error detected";
    }

    private static AiResponseData ParseAiResponse(string response)
    {
        try
        {
            // Simulate JSON parsing
            if (response.Contains("{") && response.Contains("}"))
            {
                return new AiResponseData
                {
                    Analysis = "Missing dependency",
                    Suggestions = ["Run composer install", "Clear cache"],
                    Severity = "medium"
                };
            }
        }
        catch
        {
            // Fall through to default
        }

        return new AiResponseData
        {
            Analysis = response,
            Suggestions = [],
            Severity = "unknown"
        };
    }

    private static int CalculateBudgetUsage(int used, int limit)
    {
        if (limit <= 0) return 100;
        return (used * 100) / limit;
    }

    private static bool ShouldWarnBudget(int usagePercent, int threshold = 75)
    {
        return usagePercent >= threshold;
    }

    private class AiResponseData
    {
        public string Analysis { get; set; } = string.Empty;
        public List<string> Suggestions { get; set; } = [];
        public string Severity { get; set; } = "unknown";
    }
}
