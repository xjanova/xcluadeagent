using FluentAssertions;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Tests.Core;

public class ProjectTests
{
    [Fact]
    public void Project_NewInstance_ShouldHaveDefaultValues()
    {
        // Act
        var project = new Project();

        // Assert
        project.Id.Should().NotBeEmpty();
        project.Branch.Should().Be("main");
        project.UseReleases.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Pending);
        project.Framework.Should().Be(FrameworkType.Unknown);
        project.AutoDetectFramework.Should().BeTrue();
        project.Environment.Should().Be("production");
        project.Enabled.Should().BeTrue();
        project.Config.Should().NotBeNull();
    }

    [Fact]
    public void ProjectConfig_NewInstance_ShouldHaveDefaultValues()
    {
        // Act
        var config = new ProjectConfig();

        // Assert
        config.BackupBeforeSync.Should().BeTrue();
        config.MaxBackups.Should().Be(5);
        config.AutoRollbackOnError.Should().BeTrue();
        config.RollbackTimeoutSeconds.Should().Be(60);
        config.UseDefaultCommands.Should().BeTrue();
        config.ExcludePatterns.Should().Contain(".env");
        config.ExcludePatterns.Should().Contain("node_modules/");
        config.ExcludePatterns.Should().Contain(".git/");
    }

    [Fact]
    public void Project_SetProperties_ShouldRetainValues()
    {
        // Arrange
        var project = new Project
        {
            Name = "Test Project",
            Description = "Test Description",
            Repository = "owner/repo",
            Branch = "develop",
            LocalPath = "/var/www/test",
            Environment = "staging"
        };

        // Assert
        project.Name.Should().Be("Test Project");
        project.Description.Should().Be("Test Description");
        project.Repository.Should().Be("owner/repo");
        project.Branch.Should().Be("develop");
        project.LocalPath.Should().Be("/var/www/test");
        project.Environment.Should().Be("staging");
    }

    [Fact]
    public void FilePermissions_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var permissions = new FilePermissions();

        // Assert
        permissions.Enabled.Should().BeFalse();
        permissions.DefaultFileMode.Should().Be("644");
        permissions.DefaultDirMode.Should().Be("755");
    }

    [Fact]
    public void HealthCheckConfig_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var config = new HealthCheckConfig();

        // Assert
        config.Enabled.Should().BeTrue();
        config.Method.Should().Be("GET");
        config.TimeoutSeconds.Should().Be(30);
        config.ExpectedStatusCode.Should().Be(200);
        config.RetryCount.Should().Be(3);
        config.RetryDelaySeconds.Should().Be(5);
    }
}
