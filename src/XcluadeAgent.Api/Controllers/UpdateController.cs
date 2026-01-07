using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Shared.DTOs;

namespace XcluadeAgent.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class UpdateController : ControllerBase
{
    private readonly ISelfUpdateService _updateService;
    private readonly ILogger<UpdateController> _logger;

    public UpdateController(
        ISelfUpdateService updateService,
        ILogger<UpdateController> logger)
    {
        _updateService = updateService;
        _logger = logger;
    }

    /// <summary>
    /// Get current version
    /// </summary>
    [HttpGet("version")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<VersionInfoDto>> GetVersion()
    {
        var version = _updateService.GetCurrentVersion();
        return Ok(ApiResponse<VersionInfoDto>.Ok(new VersionInfoDto
        {
            Version = version,
            BuildDate = GetBuildDate()
        }));
    }

    /// <summary>
    /// Check for available updates
    /// </summary>
    [HttpGet("check")]
    public async Task<ActionResult<ApiResponse<UpdateCheckDto>>> CheckForUpdate()
    {
        var result = await _updateService.CheckForUpdateAsync();

        var dto = new UpdateCheckDto
        {
            UpdateAvailable = result.UpdateAvailable,
            CurrentVersion = result.CurrentVersion,
            LatestVersion = result.LatestVersion,
            ReleaseNotes = result.ReleaseNotes,
            DownloadUrl = result.DownloadUrl,
            PublishedAt = result.PublishedAt,
            DownloadSizeMb = result.DownloadSize.HasValue
                ? Math.Round(result.DownloadSize.Value / 1024.0 / 1024.0, 2)
                : null,
            ErrorMessage = result.ErrorMessage,
            Assets = result.Assets.Select(a => new ReleaseAssetDto
            {
                Name = a.Name,
                DownloadUrl = a.DownloadUrl,
                SizeMb = Math.Round(a.Size / 1024.0 / 1024.0, 2)
            }).ToList()
        };

        return Ok(ApiResponse<UpdateCheckDto>.Ok(dto));
    }

    /// <summary>
    /// Apply available update
    /// </summary>
    [HttpPost("apply")]
    public async Task<ActionResult<ApiResponse<UpdateResultDto>>> ApplyUpdate([FromBody] ApplyUpdateRequest request)
    {
        if (string.IsNullOrEmpty(request.Version))
        {
            return BadRequest(ApiResponse.Fail("Version is required"));
        }

        _logger.LogInformation("Update request received for version {Version} by user {User}",
            request.Version, User.Identity?.Name);

        var result = await _updateService.ApplyUpdateAsync(request.Version);

        var dto = new UpdateResultDto
        {
            Success = result.Success,
            ErrorMessage = result.ErrorMessage,
            InstalledVersion = result.InstalledVersion,
            RestartRequired = result.RestartRequired,
            BackupPath = result.BackupPath
        };

        if (result.Success)
        {
            _logger.LogInformation("Update to version {Version} prepared successfully. Restart required.",
                result.InstalledVersion);
            return Ok(ApiResponse<UpdateResultDto>.Ok(dto, "Update prepared. Application will restart shortly."));
        }

        _logger.LogError("Update failed: {Error}", result.ErrorMessage);
        return BadRequest(ApiResponse<UpdateResultDto>.Fail(result.ErrorMessage ?? "Update failed"));
    }

    /// <summary>
    /// Get update history
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<List<UpdateHistoryDto>>>> GetUpdateHistory()
    {
        var history = await _updateService.GetUpdateHistoryAsync();

        var dtos = history.Select(h => new UpdateHistoryDto
        {
            FromVersion = h.FromVersion,
            ToVersion = h.ToVersion,
            UpdatedAt = h.UpdatedAt,
            Success = h.Success,
            ErrorMessage = h.ErrorMessage
        }).ToList();

        return Ok(ApiResponse<List<UpdateHistoryDto>>.Ok(dtos));
    }

    /// <summary>
    /// Trigger application restart (for applying pending update)
    /// </summary>
    [HttpPost("restart")]
    public ActionResult<ApiResponse> TriggerRestart()
    {
        _logger.LogWarning("Application restart requested by {User}", User.Identity?.Name);

        // Schedule restart
        Task.Run(async () =>
        {
            await Task.Delay(2000); // Give time for response to be sent

            // Execute update script if exists
            var scriptPath = Path.Combine(
                Environment.GetEnvironmentVariable("DATA_PATH") ?? "data",
                "apply-update.sh");

            if (System.IO.File.Exists(scriptPath))
            {
                _logger.LogInformation("Executing update script: {Script}", scriptPath);
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = scriptPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(startInfo);
            }
            else
            {
                // Just restart the application
                _logger.LogInformation("No update script found, performing simple restart");
                Environment.Exit(0);
            }
        });

        return Ok(ApiResponse.Ok("Application will restart in 2 seconds"));
    }

    private string GetBuildDate()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        if (assembly != null)
        {
            var buildDate = System.IO.File.GetLastWriteTime(assembly.Location);
            return buildDate.ToString("yyyy-MM-dd HH:mm:ss");
        }
        return "Unknown";
    }
}

#region DTOs

public class VersionInfoDto
{
    public string Version { get; set; } = string.Empty;
    public string BuildDate { get; set; } = string.Empty;
}

public class UpdateCheckDto
{
    public bool UpdateAvailable { get; set; }
    public string CurrentVersion { get; set; } = string.Empty;
    public string? LatestVersion { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? DownloadUrl { get; set; }
    public DateTime? PublishedAt { get; set; }
    public double? DownloadSizeMb { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ReleaseAssetDto> Assets { get; set; } = new();
}

public class ReleaseAssetDto
{
    public string Name { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public double SizeMb { get; set; }
}

public class ApplyUpdateRequest
{
    public string Version { get; set; } = string.Empty;
}

public class UpdateResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? InstalledVersion { get; set; }
    public bool RestartRequired { get; set; }
    public string? BackupPath { get; set; }
}

public class UpdateHistoryDto
{
    public string FromVersion { get; set; } = string.Empty;
    public string ToVersion { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

#endregion
