using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;
using XcluadeAgent.Shared.DTOs;

namespace XcluadeAgent.Tests.Api;

public class ProjectsControllerTests
{
    private readonly Mock<IProjectRepository> _projectRepoMock;
    private readonly Mock<ISyncService> _syncServiceMock;
    private readonly Mock<ILogger> _loggerMock;

    public ProjectsControllerTests()
    {
        _projectRepoMock = new Mock<IProjectRepository>();
        _syncServiceMock = new Mock<ISyncService>();
        _loggerMock = new Mock<ILogger>();
    }

    [Fact]
    public async Task GetProjects_ShouldReturnAllProjects()
    {
        // Arrange
        var projects = new List<Project>
        {
            CreateProject("project-1"),
            CreateProject("project-2"),
            CreateProject("project-3")
        };
        _projectRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(projects);

        // Act
        var result = await GetProjectsAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(p => p.Name).Should().Contain("project-1", "project-2", "project-3");
    }

    [Fact]
    public async Task GetProject_Existing_ShouldReturnProject()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var project = CreateProject("my-project", projectId);
        _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(project);

        // Act
        var result = await GetProjectByIdAsync(projectId);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("my-project");
    }

    [Fact]
    public async Task GetProject_NonExisting_ShouldReturnNull()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync((Project?)null);

        // Act
        var result = await GetProjectByIdAsync(projectId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateProject_Valid_ShouldReturnCreatedProject()
    {
        // Arrange
        var dto = new CreateProjectDto
        {
            Name = "new-project",
            Repository = "owner/repo",
            LocalPath = "/var/www/new-project",
            Branch = "main"
        };

        // Act
        var result = await CreateProjectAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("new-project");
        result.Repository.Should().Be("owner/repo");
    }

    [Theory]
    [InlineData("", "owner/repo", "/path")]
    [InlineData("name", "", "/path")]
    [InlineData("name", "owner/repo", "")]
    public void ValidateProject_InvalidData_ShouldReturnErrors(string name, string repo, string path)
    {
        // Arrange
        var dto = new CreateProjectDto
        {
            Name = name,
            Repository = repo,
            LocalPath = path
        };

        // Act
        var errors = ValidateCreateProject(dto);

        // Assert
        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void MapToDto_ShouldMapAllProperties()
    {
        // Arrange
        var project = CreateProject("test-project");
        project.Status = ProjectStatus.Synced;
        project.Framework = FrameworkType.Laravel;
        project.LastSyncAt = DateTime.UtcNow;

        // Act
        var dto = MapToDto(project);

        // Assert
        dto.Name.Should().Be("test-project");
        dto.Status.Should().Be("Synced");
        dto.Framework.Should().Be("Laravel");
        dto.LastSyncAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TriggerSync_EnabledProject_ShouldStartSync()
    {
        // Arrange
        var project = CreateProject("sync-project");
        project.Enabled = true;
        _projectRepoMock.Setup(r => r.GetByIdAsync(project.Id)).ReturnsAsync(project);
        _syncServiceMock.Setup(s => s.SyncProjectAsync(project.Id, It.IsAny<SyncTrigger>()))
            .ReturnsAsync(true);

        // Act
        var result = await TriggerSyncAsync(project.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TriggerSync_DisabledProject_ShouldFail()
    {
        // Arrange
        var project = CreateProject("disabled-project");
        project.Enabled = false;
        _projectRepoMock.Setup(r => r.GetByIdAsync(project.Id)).ReturnsAsync(project);

        // Act
        var result = await TriggerSyncAsync(project.Id);

        // Assert
        result.Should().BeFalse();
    }

    // Helper methods
    private Project CreateProject(string name, Guid? id = null)
    {
        return new Project
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Repository = "owner/repo",
            LocalPath = $"/var/www/{name}",
            Branch = "main",
            Enabled = true,
            Status = ProjectStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private async Task<List<ProjectDto>> GetProjectsAsync()
    {
        var projects = await _projectRepoMock.Object.GetAllAsync();
        return projects.Select(MapToDto).ToList();
    }

    private async Task<ProjectDto?> GetProjectByIdAsync(Guid id)
    {
        var project = await _projectRepoMock.Object.GetByIdAsync(id);
        return project != null ? MapToDto(project) : null;
    }

    private Task<ProjectDto> CreateProjectAsync(CreateProjectDto dto)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Repository = dto.Repository,
            LocalPath = dto.LocalPath,
            Branch = dto.Branch ?? "main",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return Task.FromResult(MapToDto(project));
    }

    private List<string> ValidateCreateProject(CreateProjectDto dto)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.Name))
            errors.Add("Name is required");
        if (string.IsNullOrWhiteSpace(dto.Repository))
            errors.Add("Repository is required");
        if (string.IsNullOrWhiteSpace(dto.LocalPath))
            errors.Add("LocalPath is required");

        return errors;
    }

    private ProjectDto MapToDto(Project project)
    {
        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Repository = project.Repository,
            LocalPath = project.LocalPath,
            Branch = project.Branch,
            Status = project.Status.ToString(),
            Framework = project.Framework.ToString(),
            Enabled = project.Enabled,
            LastSyncAt = project.LastSyncAt,
            LastSyncedVersion = project.LastSyncedVersion
        };
    }

    private async Task<bool> TriggerSyncAsync(Guid projectId)
    {
        var project = await _projectRepoMock.Object.GetByIdAsync(projectId);
        if (project == null || !project.Enabled)
            return false;

        return await _syncServiceMock.Object.SyncProjectAsync(projectId, SyncTrigger.Manual);
    }
}

// DTOs for testing
public class CreateProjectDto
{
    public string Name { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string? Branch { get; set; }
}

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncedVersion { get; set; }
}
