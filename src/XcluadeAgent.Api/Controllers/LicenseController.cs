using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;
using XcluadeAgent.Shared.DTOs;

namespace XcluadeAgent.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class LicenseController : ControllerBase
{
    private readonly ILicenseService _licenseService;
    private readonly ILogger<LicenseController> _logger;

    public LicenseController(
        ILicenseService licenseService,
        ILogger<LicenseController> logger)
    {
        _licenseService = licenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get current license information
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<LicenseInfoDto>>> GetLicense()
    {
        var info = await _licenseService.GetLicenseInfoAsync();

        var dto = new LicenseInfoDto
        {
            HasLicense = info.HasLicense,
            Type = info.Type.ToString(),
            TypeName = info.TypeName,
            LicensedTo = info.LicensedTo,
            ExpiresAt = info.ExpiresAt,
            DaysRemaining = info.DaysRemaining,
            IsExpired = info.IsExpired,
            IsExpiringSoon = info.IsExpiringSoon,
            ProjectsUsed = info.ProjectsUsed,
            ProjectsLimit = info.ProjectsLimit,
            UsersUsed = info.UsersUsed,
            UsersLimit = info.UsersLimit,
            Features = new LicenseFeaturesDto
            {
                MaxProjects = info.MaxProjects,
                MaxUsers = info.MaxUsers,
                ApiAccess = info.Features.ApiAccess,
                AiAssistant = info.Features.AiAssistant,
                MultiEnvironment = info.Features.MultiEnvironment,
                ScheduledSync = info.Features.ScheduledSync,
                AuditLogs = info.Features.AuditLogs,
                SsoIntegration = info.Features.SsoIntegration,
                PrioritySupport = info.Features.PrioritySupport,
                TeamManagement = info.Features.TeamManagement
            }
        };

        return Ok(ApiResponse<LicenseInfoDto>.Ok(dto));
    }

    /// <summary>
    /// Activate a license key
    /// </summary>
    [HttpPost("activate")]
    public async Task<ActionResult<ApiResponse<LicenseInfoDto>>> ActivateLicense([FromBody] ActivateLicenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LicenseKey))
        {
            return BadRequest(ApiResponse.Fail("License key is required"));
        }

        var result = await _licenseService.ActivateLicenseAsync(request.LicenseKey);

        if (!result.IsValid)
        {
            return BadRequest(ApiResponse.Fail(result.ErrorMessage ?? "Invalid license key"));
        }

        _logger.LogInformation("License activated: {Type}", result.License?.Type);

        var info = await _licenseService.GetLicenseInfoAsync();
        var dto = new LicenseInfoDto
        {
            HasLicense = info.HasLicense,
            Type = info.Type.ToString(),
            TypeName = info.TypeName,
            LicensedTo = info.LicensedTo,
            ExpiresAt = info.ExpiresAt,
            DaysRemaining = info.DaysRemaining,
            ProjectsUsed = info.ProjectsUsed,
            ProjectsLimit = info.ProjectsLimit,
            UsersUsed = info.UsersUsed,
            UsersLimit = info.UsersLimit
        };

        return Ok(ApiResponse<LicenseInfoDto>.Ok(dto, "License activated successfully"));
    }

    /// <summary>
    /// Deactivate current license
    /// </summary>
    [HttpPost("deactivate")]
    public async Task<ActionResult<ApiResponse>> DeactivateLicense()
    {
        var success = await _licenseService.DeactivateLicenseAsync();

        if (!success)
        {
            return BadRequest(ApiResponse.Fail("Failed to deactivate license"));
        }

        _logger.LogInformation("License deactivated");

        return Ok(ApiResponse.Ok("License deactivated successfully"));
    }

    /// <summary>
    /// Validate current license
    /// </summary>
    [HttpPost("validate")]
    public async Task<ActionResult<ApiResponse<LicenseValidationDto>>> ValidateLicense()
    {
        var result = await _licenseService.ValidateLicenseAsync();

        var dto = new LicenseValidationDto
        {
            IsValid = result.IsValid,
            ErrorMessage = result.ErrorMessage,
            Warnings = result.Warnings
        };

        return Ok(ApiResponse<LicenseValidationDto>.Ok(dto));
    }

    /// <summary>
    /// Check usage limits
    /// </summary>
    [HttpGet("limits")]
    public async Task<ActionResult<ApiResponse<LicenseLimitsDto>>> CheckLimits()
    {
        var limits = await _licenseService.CheckLimitsAsync();

        var dto = new LicenseLimitsDto
        {
            WithinLimits = limits.WithinLimits,
            CurrentProjects = limits.CurrentProjects,
            MaxProjects = limits.MaxProjects,
            CurrentUsers = limits.CurrentUsers,
            MaxUsers = limits.MaxUsers,
            Warnings = limits.LimitWarnings
        };

        return Ok(ApiResponse<LicenseLimitsDto>.Ok(dto));
    }
}

#region DTOs

public class ActivateLicenseRequest
{
    public string LicenseKey { get; set; } = string.Empty;
}

public class LicenseInfoDto
{
    public bool HasLicense { get; set; }
    public string Type { get; set; } = "Community";
    public string TypeName { get; set; } = "Community (Free)";
    public string LicensedTo { get; set; } = string.Empty;
    public string? LicenseKey { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int DaysRemaining { get; set; }
    public bool IsExpired { get; set; }
    public bool IsExpiringSoon { get; set; }
    public int ProjectsUsed { get; set; }
    public int ProjectsLimit { get; set; }
    public int UsersUsed { get; set; }
    public int UsersLimit { get; set; }
    public LicenseFeaturesDto Features { get; set; } = new();
}

public class LicenseFeaturesDto
{
    public int MaxProjects { get; set; }
    public int MaxUsers { get; set; }
    public bool ApiAccess { get; set; }
    public bool AiAssistant { get; set; }
    public bool MultiEnvironment { get; set; }
    public bool ScheduledSync { get; set; }
    public bool AuditLogs { get; set; }
    public bool SsoIntegration { get; set; }
    public bool PrioritySupport { get; set; }
    public bool TeamManagement { get; set; }
}

public class LicenseValidationDto
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class LicenseLimitsDto
{
    public bool WithinLimits { get; set; }
    public int CurrentProjects { get; set; }
    public int MaxProjects { get; set; }
    public int CurrentUsers { get; set; }
    public int MaxUsers { get; set; }
    public List<string> Warnings { get; set; } = new();
}

#endregion
