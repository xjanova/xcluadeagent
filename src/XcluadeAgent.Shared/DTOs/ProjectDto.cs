using System.ComponentModel.DataAnnotations;
using XcluadeAgent.Core.Enums;

namespace XcluadeAgent.Shared.DTOs;

/// <summary>
/// Project DTO for API responses
/// </summary>
public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Repository { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public bool UseReleases { get; set; }
    public string LocalPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusColor { get; set; } = string.Empty;
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncedVersion { get; set; }
    public string? LastError { get; set; }
    public string Framework { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Create project request
/// </summary>
public class CreateProjectRequest
{
    [Required(ErrorMessage = "Project name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Project name must be between 2 and 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Project name can only contain letters, numbers, underscores and hyphens")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Repository is required")]
    [StringLength(200, ErrorMessage = "Repository must not exceed 200 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+$", ErrorMessage = "Repository must be in format 'owner/repo'")]
    public string Repository { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Branch must not exceed 100 characters")]
    public string Branch { get; set; } = "main";

    public bool UseReleases { get; set; } = true;

    [StringLength(500, ErrorMessage = "Release tag pattern must not exceed 500 characters")]
    public string? ReleaseTagPattern { get; set; }

    [Required(ErrorMessage = "Local path is required")]
    [StringLength(1000, ErrorMessage = "Local path must not exceed 1000 characters")]
    public string LocalPath { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "Environment must not exceed 50 characters")]
    public string Environment { get; set; } = "production";

    public bool AutoDetectFramework { get; set; } = true;
    public FrameworkType? Framework { get; set; }
    public ProjectConfigDto? Config { get; set; }
}

/// <summary>
/// Update project request
/// </summary>
public class UpdateProjectRequest
{
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Project name must be between 2 and 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Project name can only contain letters, numbers, underscores and hyphens")]
    public string? Name { get; set; }

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters")]
    public string? Description { get; set; }

    [StringLength(200, ErrorMessage = "Repository must not exceed 200 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+$", ErrorMessage = "Repository must be in format 'owner/repo'")]
    public string? Repository { get; set; }

    [StringLength(100, ErrorMessage = "Branch must not exceed 100 characters")]
    public string? Branch { get; set; }

    public bool? UseReleases { get; set; }

    [StringLength(500, ErrorMessage = "Release tag pattern must not exceed 500 characters")]
    public string? ReleaseTagPattern { get; set; }

    [StringLength(1000, ErrorMessage = "Local path must not exceed 1000 characters")]
    public string? LocalPath { get; set; }

    [StringLength(50, ErrorMessage = "Environment must not exceed 50 characters")]
    public string? Environment { get; set; }

    public bool? Enabled { get; set; }
    public bool? AutoDetectFramework { get; set; }
    public FrameworkType? Framework { get; set; }
    public ProjectConfigDto? Config { get; set; }
}

/// <summary>
/// Project configuration DTO
/// </summary>
public class ProjectConfigDto
{
    public bool BackupBeforeSync { get; set; } = true;
    public int MaxBackups { get; set; } = 5;
    public bool AutoRollbackOnError { get; set; } = true;
    public int RollbackTimeoutSeconds { get; set; } = 60;
    public List<string> ExcludePatterns { get; set; } = [];
    public List<string> IncludePatterns { get; set; } = [];
    public List<string> PostDeployCommands { get; set; } = [];
    public bool UseDefaultCommands { get; set; } = true;
    public FilePermissionsDto? Permissions { get; set; }
    public HealthCheckConfigDto? HealthCheck { get; set; }
    public List<string> NotifyChannels { get; set; } = [];
    public AiMode? AiModeOverride { get; set; }
}

/// <summary>
/// File permissions DTO
/// </summary>
public class FilePermissionsDto
{
    public bool Enabled { get; set; }
    public string DefaultFileMode { get; set; } = "644";
    public string DefaultDirMode { get; set; } = "755";
    public string? Owner { get; set; }
    public string? Group { get; set; }
    public Dictionary<string, string> CustomModes { get; set; } = new();
}

/// <summary>
/// Health check configuration DTO
/// </summary>
public class HealthCheckConfigDto
{
    public bool Enabled { get; set; } = true;
    public string? Url { get; set; }
    public string Method { get; set; } = "GET";
    public int TimeoutSeconds { get; set; } = 30;
    public int ExpectedStatusCode { get; set; } = 200;
    public string? ExpectedContent { get; set; }
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
}

/// <summary>
/// Project summary for dashboard
/// </summary>
public class ProjectSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusColor { get; set; } = string.Empty;
    public string? LastSyncedVersion { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncAgo { get; set; }
    public bool NeedsSync { get; set; }
    public string LocalPath { get; set; } = string.Empty;
}
