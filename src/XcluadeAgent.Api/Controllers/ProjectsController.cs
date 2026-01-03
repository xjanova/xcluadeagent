using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;
using XcluadeAgent.Shared.DTOs;
using XcluadeAgent.Shared.Security;

namespace XcluadeAgent.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IGitHubService _gitHubService;
    private readonly ISyncService _syncService;
    private readonly ILicenseService _licenseService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        IProjectRepository projectRepository,
        ISyncHistoryRepository syncHistoryRepository,
        IGitHubService gitHubService,
        ISyncService syncService,
        ILicenseService licenseService,
        ILogger<ProjectsController> logger)
    {
        _projectRepository = projectRepository;
        _syncHistoryRepository = syncHistoryRepository;
        _gitHubService = gitHubService;
        _syncService = syncService;
        _licenseService = licenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all projects
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ProjectDto>>>> GetAll()
    {
        var projects = await _projectRepository.GetAllAsync();
        var dtos = projects.Select(MapToDto).ToList();
        return Ok(ApiResponse<List<ProjectDto>>.Ok(dtos));
    }

    /// <summary>
    /// Get project by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> GetById(Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            return NotFound(ApiResponse<ProjectDto>.Fail("Project not found"));
        }

        return Ok(ApiResponse<ProjectDto>.Ok(MapToDto(project)));
    }

    /// <summary>
    /// Create new project
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> Create([FromBody] CreateProjectRequest request)
    {
        // Check license limits
        var limits = await _licenseService.CheckLimitsAsync();
        if (!limits.WithinLimits)
        {
            return BadRequest(ApiResponse<ProjectDto>.Fail("Project limit reached. Please upgrade your license."));
        }

        // Validate repository
        var repoInfo = await _gitHubService.GetRepositoryAsync(request.Repository);
        if (repoInfo == null)
        {
            return BadRequest(ApiResponse<ProjectDto>.Fail("Repository not found or not accessible"));
        }

        // Check if name already exists
        var existing = await _projectRepository.GetByNameAsync(request.Name);
        if (existing != null)
        {
            return BadRequest(ApiResponse<ProjectDto>.Fail("A project with this name already exists"));
        }

        // Validate local path for security (path traversal prevention)
        if (!PathValidator.IsSafePath(request.LocalPath))
        {
            _logger.LogWarning("Rejected project creation with unsafe path: {Path}", request.LocalPath);
            return BadRequest(ApiResponse<ProjectDto>.Fail("Invalid local path. Path must be absolute and cannot contain traversal sequences."));
        }

        if (!PathValidator.IsPathInAllowedDirectory(request.LocalPath))
        {
            _logger.LogWarning("Rejected project creation with path outside allowed directories: {Path}", request.LocalPath);
            return BadRequest(ApiResponse<ProjectDto>.Fail("Local path must be within allowed directories (/var/www, /home, /srv, /opt, /data)."));
        }

        var project = new Project
        {
            Name = request.Name,
            Description = request.Description,
            Repository = request.Repository,
            Branch = request.Branch,
            UseReleases = request.UseReleases,
            ReleaseTagPattern = request.ReleaseTagPattern,
            LocalPath = request.LocalPath,
            Environment = request.Environment,
            AutoDetectFramework = request.AutoDetectFramework,
            Framework = request.Framework ?? FrameworkType.Unknown
        };

        if (request.Config != null)
        {
            project.Config = MapFromConfigDto(request.Config);
        }

        // Auto-detect framework if enabled
        if (project.AutoDetectFramework)
        {
            project.Framework = await DetectFrameworkAsync(request.Repository);
        }

        // Set default commands based on framework
        if (project.Config.UseDefaultCommands && project.Framework != FrameworkType.Unknown)
        {
            project.Config.PostSyncCommands = project.Framework.GetDefaultPostDeployCommands().ToList();
        }

        // Generate webhook secret
        project.WebhookSecret = GenerateWebhookSecret();

        var created = await _projectRepository.CreateAsync(project);

        _logger.LogInformation("Project created: {Name} ({Id})", created.Name, created.Id);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ApiResponse<ProjectDto>.Ok(MapToDto(created)));
    }

    /// <summary>
    /// Update project
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> Update(Guid id, [FromBody] UpdateProjectRequest request)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            return NotFound(ApiResponse<ProjectDto>.Fail("Project not found"));
        }

        // Validate local path if being updated (path traversal prevention)
        if (!string.IsNullOrEmpty(request.LocalPath))
        {
            if (!PathValidator.IsSafePath(request.LocalPath))
            {
                _logger.LogWarning("Rejected project update with unsafe path: {Path}", request.LocalPath);
                return BadRequest(ApiResponse<ProjectDto>.Fail("Invalid local path. Path must be absolute and cannot contain traversal sequences."));
            }

            if (!PathValidator.IsPathInAllowedDirectory(request.LocalPath))
            {
                _logger.LogWarning("Rejected project update with path outside allowed directories: {Path}", request.LocalPath);
                return BadRequest(ApiResponse<ProjectDto>.Fail("Local path must be within allowed directories (/var/www, /home, /srv, /opt, /data)."));
            }
        }

        // Update fields
        if (!string.IsNullOrEmpty(request.Name)) project.Name = request.Name;
        if (request.Description != null) project.Description = request.Description;
        if (!string.IsNullOrEmpty(request.Repository)) project.Repository = request.Repository;
        if (!string.IsNullOrEmpty(request.Branch)) project.Branch = request.Branch;
        if (request.UseReleases.HasValue) project.UseReleases = request.UseReleases.Value;
        if (request.ReleaseTagPattern != null) project.ReleaseTagPattern = request.ReleaseTagPattern;
        if (!string.IsNullOrEmpty(request.LocalPath)) project.LocalPath = request.LocalPath;
        if (!string.IsNullOrEmpty(request.Environment)) project.Environment = request.Environment;
        if (request.Enabled.HasValue) project.Enabled = request.Enabled.Value;
        if (request.AutoDetectFramework.HasValue) project.AutoDetectFramework = request.AutoDetectFramework.Value;
        if (request.Framework.HasValue) project.Framework = request.Framework.Value;

        if (request.Config != null)
        {
            project.Config = MapFromConfigDto(request.Config);
        }

        project.UpdatedAt = DateTime.UtcNow;

        var updated = await _projectRepository.UpdateAsync(project);

        _logger.LogInformation("Project updated: {Name} ({Id})", updated.Name, updated.Id);

        return Ok(ApiResponse<ProjectDto>.Ok(MapToDto(updated)));
    }

    /// <summary>
    /// Delete project
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            return NotFound(ApiResponse.Fail("Project not found"));
        }

        await _projectRepository.DeleteAsync(id);

        _logger.LogInformation("Project deleted: {Name} ({Id})", project.Name, id);

        return Ok(ApiResponse.Ok("Project deleted successfully"));
    }

    /// <summary>
    /// Trigger project sync
    /// </summary>
    [HttpPost("{id:guid}/sync")]
    [EnableRateLimiting("sync")]
    public async Task<ActionResult<ApiResponse<SyncHistoryDto>>> Sync(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            return NotFound(ApiResponse<SyncHistoryDto>.Fail("Project not found"));
        }

        if (!project.Enabled)
        {
            return BadRequest(ApiResponse<SyncHistoryDto>.Fail("Project is disabled"));
        }

        var username = User.Identity?.Name ?? "system";

        try
        {
            _logger.LogInformation("Sync triggered for project {Name} by {User}", project.Name, username);

            var result = await _syncService.SyncProjectAsync(id, username, cancellationToken);

            if (result.Success)
            {
                return Ok(ApiResponse<SyncHistoryDto>.Ok(MapHistoryToDto(result), "Sync completed successfully"));
            }
            else
            {
                return Ok(ApiResponse<SyncHistoryDto>.Fail(result.ErrorMessage ?? "Sync failed", MapHistoryToDto(result)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for project {Name}", project.Name);
            return StatusCode(500, ApiResponse<SyncHistoryDto>.Fail($"Sync failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Trigger project rollback
    /// </summary>
    [HttpPost("{id:guid}/rollback/{historyId:guid}")]
    [EnableRateLimiting("sync")]
    public async Task<ActionResult<ApiResponse<SyncHistoryDto>>> Rollback(Guid id, Guid historyId, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            return NotFound(ApiResponse<SyncHistoryDto>.Fail("Project not found"));
        }

        var history = await _syncHistoryRepository.GetByIdAsync(historyId);
        if (history == null || history.ProjectId != id)
        {
            return NotFound(ApiResponse<SyncHistoryDto>.Fail("Sync history not found"));
        }

        var username = User.Identity?.Name ?? "system";

        try
        {
            _logger.LogInformation("Rollback triggered for project {Name} to {Version} by {User}",
                project.Name, history.ToVersion, username);

            var result = await _syncService.RollbackProjectAsync(id, historyId, username, cancellationToken);

            if (result.Success)
            {
                return Ok(ApiResponse<SyncHistoryDto>.Ok(MapHistoryToDto(result), "Rollback completed successfully"));
            }
            else
            {
                return Ok(ApiResponse<SyncHistoryDto>.Fail(result.ErrorMessage ?? "Rollback failed", MapHistoryToDto(result)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed for project {Name}", project.Name);
            return StatusCode(500, ApiResponse<SyncHistoryDto>.Fail($"Rollback failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Get project sync history
    /// </summary>
    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<ApiResponse<List<SyncHistoryDto>>>> GetHistory(Guid id, [FromQuery] int limit = 50)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            return NotFound(ApiResponse<List<SyncHistoryDto>>.Fail("Project not found"));
        }

        var history = await _syncHistoryRepository.GetByProjectAsync(id, limit);
        var dtos = history.Select(MapHistoryToDto).ToList();

        return Ok(ApiResponse<List<SyncHistoryDto>>.Ok(dtos));
    }

    /// <summary>
    /// Get project rollback options
    /// </summary>
    [HttpGet("{id:guid}/rollback-options")]
    public async Task<ActionResult<ApiResponse<List<RollbackOptionDto>>>> GetRollbackOptions(Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            return NotFound(ApiResponse<List<RollbackOptionDto>>.Fail("Project not found"));
        }

        var history = await _syncHistoryRepository.GetByProjectAsync(id, 10);
        var options = history
            .Where(h => h.Success && !h.IsRollback)
            .Select(h => new RollbackOptionDto
            {
                HistoryId = h.Id,
                Version = h.ToVersion ?? "Unknown",
                SyncedAt = h.CompletedAt ?? h.StartedAt,
                SyncedAgo = GetTimeAgo(h.CompletedAt ?? h.StartedAt),
                HasBackup = !string.IsNullOrEmpty(h.BackupPath)
            })
            .ToList();

        return Ok(ApiResponse<List<RollbackOptionDto>>.Ok(options));
    }

    /// <summary>
    /// Get webhook URL for project
    /// </summary>
    [HttpGet("{id:guid}/webhook")]
    public async Task<ActionResult<ApiResponse<object>>> GetWebhook(Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            return NotFound(ApiResponse<object>.Fail("Project not found"));
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var webhookUrl = $"{baseUrl}/webhook/github?project={id}";

        return Ok(ApiResponse<object>.Ok(new
        {
            url = webhookUrl,
            secret = project.WebhookSecret,
            events = new[] { "release" }
        }));
    }

    /// <summary>
    /// Regenerate webhook secret
    /// </summary>
    [HttpPost("{id:guid}/webhook/regenerate")]
    public async Task<ActionResult<ApiResponse<object>>> RegenerateWebhookSecret(Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            return NotFound(ApiResponse<object>.Fail("Project not found"));
        }

        project.WebhookSecret = GenerateWebhookSecret();
        project.UpdatedAt = DateTime.UtcNow;
        await _projectRepository.UpdateAsync(project);

        return Ok(ApiResponse<object>.Ok(new { secret = project.WebhookSecret }));
    }

    #region Private Methods

    private static ProjectDto MapToDto(Project project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        Description = project.Description,
        Repository = project.Repository,
        Branch = project.Branch,
        UseReleases = project.UseReleases,
        LocalPath = project.LocalPath,
        Status = project.Status.ToString(),
        StatusColor = GetStatusColor(project.Status),
        LastSyncAt = project.LastSyncedAt,
        LastSyncedVersion = project.LastSyncedVersion,
        LastError = project.LastError,
        Framework = project.Framework.GetDisplayName(),
        Environment = project.Environment,
        Enabled = project.Enabled,
        CreatedAt = project.CreatedAt,
        UpdatedAt = project.UpdatedAt
    };

    private static SyncHistoryDto MapHistoryToDto(SyncHistory history) => new()
    {
        Id = history.Id,
        ProjectId = history.ProjectId,
        ProjectName = history.ProjectName,
        Trigger = history.Trigger.ToString(),
        InitiatedBy = history.InitiatedBy,
        FromVersion = history.FromVersion,
        ToVersion = history.ToVersion,
        StartedAt = history.StartedAt,
        CompletedAt = history.CompletedAt,
        Duration = FormatDuration(history.DurationMs),
        Success = history.Success,
        ErrorMessage = history.ErrorMessage,
        FilesChanged = history.FilesChanged,
        BytesTransferred = FormatBytes(history.BytesTransferred),
        IsRollback = history.IsRollback,
        HasBackup = !string.IsNullOrEmpty(history.BackupPath)
    };

    private static ProjectConfig MapFromConfigDto(ProjectConfigDto dto) => new()
    {
        Backup = new ProjectBackupConfig
        {
            Enabled = dto.BackupBeforeSync,
            RetentionCount = dto.MaxBackups
        },
        AutoRollbackOnError = dto.AutoRollbackOnError,
        RollbackTimeoutSeconds = dto.RollbackTimeoutSeconds,
        ExcludePaths = dto.ExcludePatterns,
        IncludePaths = dto.IncludePatterns,
        PostSyncCommands = dto.PostDeployCommands,
        UseDefaultCommands = dto.UseDefaultCommands,
        AiModeOverride = dto.AiModeOverride
    };

    private static string GetStatusColor(ProjectStatus status) => status switch
    {
        ProjectStatus.Synced => "green",
        ProjectStatus.Pending or ProjectStatus.Syncing => "yellow",
        ProjectStatus.Failed => "red",
        ProjectStatus.Disabled => "gray",
        _ => "gray"
    };

    private async Task<FrameworkType> DetectFrameworkAsync(string repository)
    {
        // TODO: Implement actual detection by checking files in repository
        return FrameworkType.Unknown;
    }

    private static string GenerateWebhookSecret()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string FormatDuration(long? ms)
    {
        if (!ms.HasValue) return "-";
        if (ms < 1000) return $"{ms}ms";
        if (ms < 60000) return $"{ms / 1000.0:F1}s";
        return $"{ms / 60000.0:F1}m";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    private static string GetTimeAgo(DateTime time)
    {
        var diff = DateTime.UtcNow - time;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hours ago";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays} days ago";
        return time.ToString("MMM dd, yyyy");
    }

    #endregion
}
