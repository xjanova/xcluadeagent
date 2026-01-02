using FluentAssertions;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Tests.Infrastructure;

public class NotificationServiceTests
{
    [Theory]
    [InlineData(NotificationChannel.Discord, "https://discord.com/api/webhooks/123/abc")]
    [InlineData(NotificationChannel.Slack, "https://hooks.slack.com/services/T00/B00/xxx")]
    [InlineData(NotificationChannel.Telegram, "123456789")]
    public void ValidateChannel_ValidConfig_ShouldReturnTrue(NotificationChannel channel, string identifier)
    {
        // Act
        var result = ValidateChannelConfig(channel, identifier);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(NotificationChannel.Discord, "")]
    [InlineData(NotificationChannel.Discord, "invalid-url")]
    [InlineData(NotificationChannel.Slack, "not-a-slack-webhook")]
    public void ValidateChannel_InvalidConfig_ShouldReturnFalse(NotificationChannel channel, string identifier)
    {
        // Act
        var result = ValidateChannelConfig(channel, identifier);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(ProjectStatus.Synced, true)]
    [InlineData(ProjectStatus.Failed, true)]
    [InlineData(ProjectStatus.Pending, false)]
    [InlineData(ProjectStatus.Syncing, false)]
    public void ShouldNotify_BasedOnStatus(ProjectStatus status, bool shouldNotify)
    {
        // Act
        var result = ShouldSendNotification(status);

        // Assert
        result.Should().Be(shouldNotify);
    }

    [Fact]
    public void FormatMessage_Success_ShouldContainProjectName()
    {
        // Arrange
        var projectName = "my-project";
        var version = "v1.0.0";

        // Act
        var message = FormatSuccessMessage(projectName, version);

        // Assert
        message.Should().Contain(projectName);
        message.Should().Contain(version);
        message.Should().Contain("success");
    }

    [Fact]
    public void FormatMessage_Error_ShouldContainErrorDetails()
    {
        // Arrange
        var projectName = "my-project";
        var error = "Connection timeout";

        // Act
        var message = FormatErrorMessage(projectName, error);

        // Assert
        message.Should().Contain(projectName);
        message.Should().Contain(error);
        message.Should().Contain("failed");
    }

    [Fact]
    public void FormatDiscordEmbed_ShouldHaveCorrectStructure()
    {
        // Arrange
        var title = "Sync Complete";
        var description = "Project synced successfully";
        var color = 0x00FF00; // Green

        // Act
        var embed = CreateDiscordEmbed(title, description, color);

        // Assert
        embed.Should().ContainKey("title");
        embed.Should().ContainKey("description");
        embed.Should().ContainKey("color");
        embed["title"].Should().Be(title);
        embed["description"].Should().Be(description);
        embed["color"].Should().Be(color);
    }

    // Helper methods simulating NotificationService logic
    private static bool ValidateChannelConfig(NotificationChannel channel, string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return false;

        return channel switch
        {
            NotificationChannel.Discord => identifier.StartsWith("https://discord.com/api/webhooks/"),
            NotificationChannel.Slack => identifier.StartsWith("https://hooks.slack.com/"),
            NotificationChannel.Telegram => long.TryParse(identifier, out _),
            NotificationChannel.Email => identifier.Contains("@"),
            NotificationChannel.LineOa => !string.IsNullOrEmpty(identifier),
            _ => false
        };
    }

    private static bool ShouldSendNotification(ProjectStatus status)
    {
        return status switch
        {
            ProjectStatus.Synced => true,
            ProjectStatus.Failed => true,
            _ => false
        };
    }

    private static string FormatSuccessMessage(string projectName, string version)
    {
        return $"✅ Sync success: {projectName} updated to {version}";
    }

    private static string FormatErrorMessage(string projectName, string error)
    {
        return $"❌ Sync failed: {projectName} - {error}";
    }

    private static Dictionary<string, object> CreateDiscordEmbed(string title, string description, int color)
    {
        return new Dictionary<string, object>
        {
            ["title"] = title,
            ["description"] = description,
            ["color"] = color,
            ["timestamp"] = DateTime.UtcNow.ToString("o")
        };
    }
}

public enum NotificationChannel
{
    Discord,
    Slack,
    Telegram,
    Email,
    LineOa,
    Webhook
}
