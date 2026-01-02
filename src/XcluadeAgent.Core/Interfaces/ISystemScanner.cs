using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Core.Interfaces;

/// <summary>
/// Service for comprehensive system scanning and analysis
/// </summary>
public interface ISystemScanner
{
    /// <summary>
    /// Perform full system scan
    /// </summary>
    Task<SystemInfo> ScanSystemAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Quick scan for basic info
    /// </summary>
    Task<SystemInfo> QuickScanAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if running as root
    /// </summary>
    bool IsRunningAsRoot();

    /// <summary>
    /// Get last scan result
    /// </summary>
    SystemInfo? GetLastScanResult();
}

/// <summary>
/// Service for detecting and analyzing web servers
/// </summary>
public interface IWebServerDetector
{
    /// <summary>
    /// Detect all installed web servers
    /// </summary>
    Task<List<WebServerInfo>> DetectWebServersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get primary/active web server
    /// </summary>
    Task<WebServerInfo?> GetPrimaryWebServerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if specific web server is installed
    /// </summary>
    Task<bool> IsWebServerInstalledAsync(WebServerType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get web server configuration
    /// </summary>
    Task<WebServerInfo?> GetWebServerInfoAsync(WebServerType type, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for discovering websites/virtual hosts
/// </summary>
public interface IWebsiteDiscovery
{
    /// <summary>
    /// Discover all websites from all detected web servers
    /// </summary>
    Task<List<DiscoveredSite>> DiscoverAllSitesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discover sites from specific web server
    /// </summary>
    Task<List<DiscoveredSite>> DiscoverSitesAsync(WebServerInfo webServer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze a specific site directory
    /// </summary>
    Task<DiscoveredSite> AnalyzeSiteAsync(string documentRoot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Match discovered sites with GitHub repositories
    /// </summary>
    Task<List<SiteRepoMatch>> MatchSitesWithReposAsync(List<DiscoveredSite> sites, List<string> repositories, CancellationToken cancellationToken = default);
}

public class SiteRepoMatch
{
    public DiscoveredSite Site { get; set; } = null!;
    public string? MatchedRepository { get; set; }
    public double Confidence { get; set; }
    public MatchMethod Method { get; set; }
    public string Reason { get; set; } = "";
}

public enum MatchMethod
{
    None,
    GitRemote,          // Matched via .git/config remote URL
    DirectoryName,      // Matched via directory name
    PackageJson,        // Matched via package.json name
    ComposerJson,       // Matched via composer.json name
    Readme,             // Matched via README content
    AiAnalysis          // Matched via AI analysis
}

/// <summary>
/// Service for managing file/directory permissions
/// </summary>
public interface IPermissionManager
{
    /// <summary>
    /// Get current ownership of path
    /// </summary>
    Task<(string user, string group)> GetOwnershipAsync(string path);

    /// <summary>
    /// Set ownership while preserving web server access
    /// </summary>
    Task SetOwnershipAsync(string path, string user, string group, bool recursive = true);

    /// <summary>
    /// Get recommended ownership for web server
    /// </summary>
    Task<(string user, string group)> GetRecommendedOwnershipAsync(WebServerInfo webServer);

    /// <summary>
    /// Fix permissions for a site directory
    /// </summary>
    Task FixPermissionsAsync(string path, string user, string group, int fileMode = 644, int dirMode = 755);

    /// <summary>
    /// Validate permissions are correct
    /// </summary>
    Task<List<PermissionIssue>> ValidatePermissionsAsync(string path, string expectedUser, string expectedGroup);
}

public class PermissionIssue
{
    public string Path { get; set; } = "";
    public string Issue { get; set; } = "";
    public string CurrentOwner { get; set; } = "";
    public string ExpectedOwner { get; set; } = "";
    public int CurrentMode { get; set; }
    public int ExpectedMode { get; set; }
}

/// <summary>
/// Service for AI-powered conflict resolution
/// </summary>
public interface IConflictResolver
{
    /// <summary>
    /// Analyze conflicts and get AI recommendations
    /// </summary>
    Task<List<SystemConflict>> AnalyzeConflictsAsync(SystemInfo systemInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get AI recommendation for specific conflict
    /// </summary>
    Task<string> GetAiRecommendationAsync(SystemConflict conflict, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply resolution to conflict
    /// </summary>
    Task<bool> ApplyResolutionAsync(SystemConflict conflict, string resolutionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-resolve all safe conflicts
    /// </summary>
    Task<int> AutoResolveAsync(List<SystemConflict> conflicts, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for environment detection
/// </summary>
public interface IEnvironmentAnalyzer
{
    /// <summary>
    /// Detect overall environment type
    /// </summary>
    Task<EnvironmentType> DetectEnvironmentTypeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect site-specific environment
    /// </summary>
    Task<SiteEnvironment> DetectSiteEnvironmentAsync(string documentRoot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get environment indicators for a path
    /// </summary>
    Task<List<string>> GetEnvironmentIndicatorsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if running in container
    /// </summary>
    bool IsRunningInContainer();

    /// <summary>
    /// Check if running in VM
    /// </summary>
    Task<bool> IsRunningInVmAsync();

    /// <summary>
    /// Detect cloud provider
    /// </summary>
    Task<string?> DetectCloudProviderAsync();
}
