using FluentAssertions;
using Xunit;
using XcluadeAgent.Core.Enums;

namespace XcluadeAgent.Tests.Core;

public class FrameworkTypeTests
{
    [Theory]
    [InlineData(FrameworkType.Laravel, "Laravel (PHP)")]
    [InlineData(FrameworkType.React, "React")]
    [InlineData(FrameworkType.Vue, "Vue.js")]
    [InlineData(FrameworkType.Django, "Django (Python)")]
    [InlineData(FrameworkType.DotNet, ".NET")]
    [InlineData(FrameworkType.NodeJs, "Node.js")]
    [InlineData(FrameworkType.Static, "Static HTML")]
    public void GetDisplayName_ShouldReturnCorrectName(FrameworkType type, string expectedName)
    {
        // Act
        var displayName = type.GetDisplayName();

        // Assert
        displayName.Should().Be(expectedName);
    }

    [Fact]
    public void GetDefaultPostDeployCommands_Laravel_ShouldReturnCorrectCommands()
    {
        // Arrange
        var framework = FrameworkType.Laravel;

        // Act
        var commands = framework.GetDefaultPostDeployCommands();

        // Assert
        commands.Should().Contain(c => c.Contains("composer install"));
        commands.Should().Contain(c => c.Contains("artisan migrate"));
        commands.Should().Contain(c => c.Contains("artisan config:cache"));
    }

    [Fact]
    public void GetDefaultPostDeployCommands_NodeJs_ShouldReturnCorrectCommands()
    {
        // Arrange
        var framework = FrameworkType.NodeJs;

        // Act
        var commands = framework.GetDefaultPostDeployCommands();

        // Assert
        commands.Should().Contain(c => c.Contains("npm ci"));
        commands.Should().Contain(c => c.Contains("npm run build"));
    }

    [Fact]
    public void GetDefaultPostDeployCommands_Django_ShouldReturnCorrectCommands()
    {
        // Arrange
        var framework = FrameworkType.Django;

        // Act
        var commands = framework.GetDefaultPostDeployCommands();

        // Assert
        commands.Should().Contain(c => c.Contains("pip install"));
        commands.Should().Contain(c => c.Contains("manage.py migrate"));
        commands.Should().Contain(c => c.Contains("collectstatic"));
    }

    [Fact]
    public void GetDefaultPostDeployCommands_Static_ShouldReturnEmptyOrMinimal()
    {
        // Arrange
        var framework = FrameworkType.Static;

        // Act
        var commands = framework.GetDefaultPostDeployCommands();

        // Assert
        commands.Should().BeEmpty();
    }

    [Fact]
    public void GetDefaultPostDeployCommands_Unknown_ShouldReturnEmpty()
    {
        // Arrange
        var framework = FrameworkType.Unknown;

        // Act
        var commands = framework.GetDefaultPostDeployCommands();

        // Assert
        commands.Should().BeEmpty();
    }

    [Fact]
    public void AllFrameworkTypes_ShouldHaveDisplayName()
    {
        // Arrange
        var allTypes = Enum.GetValues<FrameworkType>();

        // Act & Assert
        foreach (var type in allTypes)
        {
            var displayName = type.GetDisplayName();
            displayName.Should().NotBeNullOrEmpty($"Framework {type} should have a display name");
        }
    }
}
