using FluentAssertions;
using Xunit;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Tests.Core;

public class LicenseTests
{
    [Theory]
    [InlineData(LicenseType.Community, 3, 1)]
    [InlineData(LicenseType.Professional, 10, 5)]
    [InlineData(LicenseType.Enterprise, 50, 25)]
    [InlineData(LicenseType.Unlimited, int.MaxValue, int.MaxValue)]
    public void GetLimits_ShouldReturnCorrectValues(LicenseType type, int expectedProjects, int expectedUsers)
    {
        // Act
        var (projects, users) = LicenseLimits.GetLimits(type);

        // Assert
        projects.Should().Be(expectedProjects);
        users.Should().Be(expectedUsers);
    }

    [Fact]
    public void LicenseFeatures_Community_ShouldHaveBasicFeatures()
    {
        // Act
        var features = LicenseFeatures.GetDefault(LicenseType.Community);

        // Assert - Basic features should be enabled
        features.Sync.Should().BeTrue();
        features.Backup.Should().BeTrue();
        features.Rollback.Should().BeTrue();
        features.WebDashboard.Should().BeTrue();
        features.Cli.Should().BeTrue();

        // Advanced features should be disabled
        features.AiAssistant.Should().BeFalse();
        features.TeamManagement.Should().BeFalse();
        features.SsoIntegration.Should().BeFalse();
    }

    [Fact]
    public void LicenseFeatures_Professional_ShouldHaveProFeatures()
    {
        // Act
        var features = LicenseFeatures.GetDefault(LicenseType.Professional);

        // Assert - All basic + pro features
        features.Sync.Should().BeTrue();
        features.MultiEnvironment.Should().BeTrue();
        features.ScheduledSync.Should().BeTrue();
        features.AdvancedNotifications.Should().BeTrue();
        features.CustomBranding.Should().BeTrue();

        // Enterprise features still disabled
        features.AiAssistant.Should().BeFalse();
        features.TeamManagement.Should().BeFalse();
    }

    [Fact]
    public void LicenseFeatures_Enterprise_ShouldHaveAllFeatures()
    {
        // Act
        var features = LicenseFeatures.GetDefault(LicenseType.Enterprise);

        // Assert - All features enabled
        features.Sync.Should().BeTrue();
        features.AiAssistant.Should().BeTrue();
        features.TeamManagement.Should().BeTrue();
        features.AuditLogs.Should().BeTrue();
        features.ApiAccess.Should().BeTrue();
        features.SsoIntegration.Should().BeTrue();
        features.PrioritySupport.Should().BeTrue();
    }

    [Fact]
    public void LicenseInfo_Community_ShouldReturnCorrectDefaults()
    {
        // Act
        var info = LicenseInfo.Community();

        // Assert
        info.HasLicense.Should().BeFalse();
        info.Type.Should().Be(LicenseType.Community);
        info.TypeName.Should().Be("Community (Free)");
        info.ProjectsLimit.Should().Be(3);
        info.UsersLimit.Should().Be(1);
    }

    [Theory]
    [InlineData(LicenseType.Community, "Community (Free)")]
    [InlineData(LicenseType.Professional, "Professional")]
    [InlineData(LicenseType.Enterprise, "Enterprise")]
    [InlineData(LicenseType.Unlimited, "Unlimited")]
    public void LicenseTypeNames_GetName_ShouldReturnCorrectName(LicenseType type, string expected)
    {
        // Act
        var name = LicenseTypeNames.GetName(type);

        // Assert
        name.Should().Be(expected);
    }

    [Theory]
    [InlineData(LicenseType.Community, "gray")]
    [InlineData(LicenseType.Professional, "blue")]
    [InlineData(LicenseType.Enterprise, "purple")]
    [InlineData(LicenseType.Unlimited, "gold")]
    public void LicenseTypeNames_GetBadgeColor_ShouldReturnCorrectColor(LicenseType type, string expected)
    {
        // Act
        var color = LicenseTypeNames.GetBadgeColor(type);

        // Assert
        color.Should().Be(expected);
    }
}
