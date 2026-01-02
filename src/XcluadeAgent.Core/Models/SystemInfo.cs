using XcluadeAgent.Core.Enums;

namespace XcluadeAgent.Core.Models;

/// <summary>
/// Complete system environment information
/// </summary>
public class SystemInfo
{
    public string Hostname { get; set; } = "";
    public string OsName { get; set; } = "";
    public string OsVersion { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string Kernel { get; set; } = "";

    // User & Permissions
    public bool IsRoot { get; set; }
    public int CurrentUserId { get; set; }
    public string CurrentUsername { get; set; } = "";

    // Web Server
    public WebServerInfo? WebServer { get; set; }
    public List<WebServerInfo> DetectedWebServers { get; set; } = new();

    // Runtime Environments
    public List<RuntimeInfo> Runtimes { get; set; } = new();

    // Discovered Sites
    public List<DiscoveredSite> DiscoveredSites { get; set; } = new();

    // System Resources
    public long TotalMemoryMb { get; set; }
    public long AvailableMemoryMb { get; set; }
    public long TotalDiskGb { get; set; }
    public long AvailableDiskGb { get; set; }

    // Environment Type Detection
    public EnvironmentType DetectedEnvironment { get; set; }
    public List<string> EnvironmentIndicators { get; set; } = new();

    // Conflicts & Warnings
    public List<SystemConflict> Conflicts { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    // Scan Metadata
    public DateTime ScannedAt { get; set; }
    public TimeSpan ScanDuration { get; set; }
}

public class WebServerInfo
{
    public WebServerType Type { get; set; }
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string ConfigPath { get; set; } = "";
    public string SitesPath { get; set; } = "";
    public string LogPath { get; set; } = "";
    public bool IsRunning { get; set; }
    public bool IsEnabled { get; set; }
    public string ServiceName { get; set; } = "";
    public string RunAsUser { get; set; } = "";
    public string RunAsGroup { get; set; } = "";
    public int? ProcessId { get; set; }
    public List<int> ListeningPorts { get; set; } = new();
}

public enum WebServerType
{
    Unknown,
    Nginx,
    Apache,
    Caddy,
    LiteSpeed,
    OpenLiteSpeed,
    IIS,
    Traefik,
    HAProxy
}

public class RuntimeInfo
{
    public RuntimeType Type { get; set; }
    public string Version { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsDefault { get; set; }
}

public enum RuntimeType
{
    Unknown,
    Php,
    NodeJs,
    Python,
    DotNet,
    Ruby,
    Go,
    Java,
    Rust
}

public class DiscoveredSite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Domain { get; set; } = "";
    public List<string> Aliases { get; set; } = new();
    public string DocumentRoot { get; set; } = "";
    public string ConfigFile { get; set; } = "";

    // Detection
    public WebServerType WebServer { get; set; }
    public FrameworkType DetectedFramework { get; set; }
    public string? DetectedFrameworkVersion { get; set; }

    // Permissions
    public string OwnerUser { get; set; } = "";
    public string OwnerGroup { get; set; } = "";
    public int FilePermissions { get; set; }
    public int DirectoryPermissions { get; set; }

    // Environment
    public SiteEnvironment Environment { get; set; }
    public List<string> EnvironmentIndicators { get; set; } = new();

    // Status
    public bool IsEnabled { get; set; }
    public bool HasSsl { get; set; }
    public string? SslCertPath { get; set; }
    public DateTime? SslExpiresAt { get; set; }

    // Git Info (if exists)
    public bool HasGitRepo { get; set; }
    public string? GitRemoteUrl { get; set; }
    public string? GitBranch { get; set; }
    public string? LastCommitHash { get; set; }

    // Matching
    public Guid? MatchedProjectId { get; set; }
    public double MatchConfidence { get; set; }
    public string? MatchReason { get; set; }
}

public enum SiteEnvironment
{
    Unknown,
    Production,
    Staging,
    Development,
    Testing,
    Local
}

public enum EnvironmentType
{
    Unknown,
    Production,       // Real production server
    Staging,          // Staging/Pre-production
    Development,      // Development server
    Local,            // Local machine
    Container,        // Docker/Container
    VirtualMachine,   // VM
    Cloud             // Cloud instance (AWS, GCP, Azure)
}

public class SystemConflict
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public ConflictType Type { get; set; }
    public ConflictSeverity Severity { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> AffectedItems { get; set; } = new();
    public List<ConflictResolution> SuggestedResolutions { get; set; } = new();
    public string? AiRecommendation { get; set; }
    public bool IsResolved { get; set; }
    public string? ResolutionApplied { get; set; }
}

public enum ConflictType
{
    MultipleWebServers,
    PortConflict,
    PermissionMismatch,
    OwnershipConflict,
    ConfigurationConflict,
    VersionMismatch,
    MissingDependency,
    SecurityRisk,
    ResourceLimit
}

public enum ConflictSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public class ConflictResolution
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Command { get; set; }
    public bool RequiresRestart { get; set; }
    public bool IsRecommended { get; set; }
    public int Risk { get; set; } // 1-10
}
