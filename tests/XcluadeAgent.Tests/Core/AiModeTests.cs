using FluentAssertions;
using XcluadeAgent.Core.Enums;

namespace XcluadeAgent.Tests.Core;

public class AiModeTests
{
    [Theory]
    [InlineData(AiMode.Off, "Off - AI Disabled")]
    [InlineData(AiMode.Alert, "Level 1: Alert Only")]
    [InlineData(AiMode.Suggest, "Level 2: Suggest Fix")]
    [InlineData(AiMode.Review, "Level 3: Fix + Review")]
    [InlineData(AiMode.Sandbox, "Level 4: Auto-Fix Sandbox")]
    [InlineData(AiMode.Auto, "Level 5: Full Auto-Fix")]
    public void GetDisplayName_ShouldReturnCorrectName(AiMode mode, string expectedName)
    {
        // Act
        var displayName = mode.GetDisplayName();

        // Assert
        displayName.Should().Be(expectedName);
    }

    [Theory]
    [InlineData(AiMode.Off, "None")]
    [InlineData(AiMode.Alert, "Very Low")]
    [InlineData(AiMode.Suggest, "Low")]
    [InlineData(AiMode.Review, "Medium")]
    [InlineData(AiMode.Sandbox, "Medium")]
    [InlineData(AiMode.Auto, "HIGH")]
    public void GetRiskLevel_ShouldReturnCorrectRisk(AiMode mode, string expectedRisk)
    {
        // Act
        var riskLevel = mode.GetRiskLevel();

        // Assert
        riskLevel.Should().Be(expectedRisk);
    }

    [Theory]
    [InlineData(AiMode.Off, false)]
    [InlineData(AiMode.Alert, false)]
    [InlineData(AiMode.Suggest, false)]
    [InlineData(AiMode.Review, true)]
    [InlineData(AiMode.Sandbox, false)]
    [InlineData(AiMode.Auto, false)]
    public void RequiresApproval_ShouldReturnCorrectValue(AiMode mode, bool expected)
    {
        // Act
        var requiresApproval = mode.RequiresApproval();

        // Assert
        requiresApproval.Should().Be(expected);
    }

    [Theory]
    [InlineData(AiMode.Off, false)]
    [InlineData(AiMode.Alert, false)]
    [InlineData(AiMode.Suggest, false)]
    [InlineData(AiMode.Review, true)]
    [InlineData(AiMode.Sandbox, true)]
    [InlineData(AiMode.Auto, true)]
    public void CanModifyCode_ShouldReturnCorrectValue(AiMode mode, bool expected)
    {
        // Act
        var canModify = mode.CanModifyCode();

        // Assert
        canModify.Should().Be(expected);
    }

    [Fact]
    public void AllAiModes_ShouldHaveDescription()
    {
        // Arrange
        var allModes = Enum.GetValues<AiMode>();

        // Act & Assert
        foreach (var mode in allModes)
        {
            var description = mode.GetDescription();
            description.Should().NotBeNullOrEmpty($"Mode {mode} should have a description");
        }
    }

    [Fact]
    public void AllAiModes_ShouldHaveRiskColor()
    {
        // Arrange
        var allModes = Enum.GetValues<AiMode>();
        var validColors = new[] { "gray", "green", "blue", "yellow", "orange", "red" };

        // Act & Assert
        foreach (var mode in allModes)
        {
            var color = mode.GetRiskColor();
            color.Should().BeOneOf(validColors, $"Mode {mode} should have a valid risk color");
        }
    }
}
