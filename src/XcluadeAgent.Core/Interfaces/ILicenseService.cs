using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Core.Interfaces;

/// <summary>
/// Service for license management
/// </summary>
public interface ILicenseService
{
    /// <summary>
    /// Get current license
    /// </summary>
    Task<License?> GetCurrentLicenseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate and activate a license key
    /// </summary>
    Task<LicenseValidationResult> ActivateLicenseAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate current license
    /// </summary>
    Task<LicenseValidationResult> ValidateLicenseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivate current license
    /// </summary>
    Task<bool> DeactivateLicenseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a feature is available
    /// </summary>
    Task<bool> IsFeatureAvailableAsync(string featureName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if within limits
    /// </summary>
    Task<LimitCheck> CheckLimitsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get license info for display
    /// </summary>
    Task<LicenseInfo> GetLicenseInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh license from server
    /// </summary>
    Task<LicenseValidationResult> RefreshLicenseAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// License validation result
/// </summary>
public class LicenseValidationResult
{
    public bool IsValid { get; set; }
    public License? License { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// License limit check result
/// </summary>
public class LimitCheck
{
    public bool WithinLimits { get; set; }
    public int CurrentProjects { get; set; }
    public int MaxProjects { get; set; }
    public int CurrentUsers { get; set; }
    public int MaxUsers { get; set; }
    public List<string> LimitWarnings { get; set; } = [];
}

/// <summary>
/// License info for display
/// </summary>
public class LicenseInfo
{
    public bool HasLicense { get; set; }
    public LicenseType Type { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string LicensedTo { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public int DaysRemaining { get; set; }
    public bool IsExpired { get; set; }
    public bool IsExpiringSoon { get; set; }
    public int ProjectsUsed { get; set; }
    public int ProjectsLimit { get; set; }
    public int UsersUsed { get; set; }
    public int UsersLimit { get; set; }
    public LicenseFeatures Features { get; set; } = new();

    public static LicenseInfo Community() => new()
    {
        HasLicense = false,
        Type = LicenseType.Community,
        TypeName = "Community (Free)",
        LicensedTo = "Community Edition",
        ProjectsLimit = 3,
        UsersLimit = 1,
        Features = LicenseFeatures.GetDefault(LicenseType.Community)
    };
}

/// <summary>
/// License type display names
/// </summary>
public static class LicenseTypeNames
{
    public static string GetName(LicenseType type) => type switch
    {
        LicenseType.Community => "Community (Free)",
        LicenseType.Professional => "Professional",
        LicenseType.Enterprise => "Enterprise",
        LicenseType.Unlimited => "Unlimited",
        _ => "Unknown"
    };

    public static string GetBadgeColor(LicenseType type) => type switch
    {
        LicenseType.Community => "gray",
        LicenseType.Professional => "blue",
        LicenseType.Enterprise => "purple",
        LicenseType.Unlimited => "gold",
        _ => "gray"
    };
}
