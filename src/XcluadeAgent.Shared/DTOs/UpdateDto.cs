namespace XcluadeAgent.Shared.DTOs;

/// <summary>
/// Version information
/// </summary>
public class VersionInfoDto
{
    public string Version { get; set; } = string.Empty;
    public string BuildDate { get; set; } = string.Empty;
}

/// <summary>
/// Update check response
/// </summary>
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

/// <summary>
/// Release asset information
/// </summary>
public class ReleaseAssetDto
{
    public string Name { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public double SizeMb { get; set; }
}

/// <summary>
/// Apply update request
/// </summary>
public class ApplyUpdateRequest
{
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Update result
/// </summary>
public class UpdateResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? InstalledVersion { get; set; }
    public bool RestartRequired { get; set; }
    public string? BackupPath { get; set; }
}

/// <summary>
/// Update history entry
/// </summary>
public class UpdateHistoryDto
{
    public string FromVersion { get; set; } = string.Empty;
    public string ToVersion { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
