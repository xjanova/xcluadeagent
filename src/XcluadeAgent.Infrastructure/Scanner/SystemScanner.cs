using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.Scanner;

public class SystemScanner : ISystemScanner
{
    private readonly IWebServerDetector _webServerDetector;
    private readonly IWebsiteDiscovery _websiteDiscovery;
    private readonly IEnvironmentAnalyzer _environmentAnalyzer;
    private readonly IConflictResolver _conflictResolver;
    private readonly ILogger<SystemScanner> _logger;
    private SystemInfo? _lastScanResult;

    public SystemScanner(
        IWebServerDetector webServerDetector,
        IWebsiteDiscovery websiteDiscovery,
        IEnvironmentAnalyzer environmentAnalyzer,
        IConflictResolver conflictResolver,
        ILogger<SystemScanner> logger)
    {
        _webServerDetector = webServerDetector;
        _websiteDiscovery = websiteDiscovery;
        _environmentAnalyzer = environmentAnalyzer;
        _conflictResolver = conflictResolver;
        _logger = logger;
    }

    public bool IsRunningAsRoot()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // On Windows, check if running as Administrator
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        // On Unix, check if UID is 0 (root)
        return Environment.GetEnvironmentVariable("EUID") == "0" ||
               GetUnixUserId() == 0;
    }

    private int GetUnixUserId()
    {
        try
        {
            var result = ExecuteCommand("id", "-u");
            if (int.TryParse(result.Trim(), out var uid))
                return uid;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get Unix user ID");
        }
        return -1;
    }

    public async Task<SystemInfo> ScanSystemAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting full system scan...");

        var systemInfo = new SystemInfo
        {
            ScannedAt = DateTime.UtcNow
        };

        try
        {
            // Basic OS Info
            await ScanOsInfoAsync(systemInfo);
            _logger.LogDebug("OS info scanned: {Os} {Version}", systemInfo.OsName, systemInfo.OsVersion);

            // Check root status
            systemInfo.IsRoot = IsRunningAsRoot();
            systemInfo.CurrentUserId = GetUnixUserId();
            systemInfo.CurrentUsername = Environment.UserName;

            if (!systemInfo.IsRoot)
            {
                systemInfo.Warnings.Add("Not running as root. Some features may be limited.");
            }

            // Detect web servers
            _logger.LogInformation("Detecting web servers...");
            systemInfo.DetectedWebServers = await _webServerDetector.DetectWebServersAsync(cancellationToken);
            systemInfo.WebServer = await _webServerDetector.GetPrimaryWebServerAsync(cancellationToken);

            if (systemInfo.DetectedWebServers.Count > 1)
            {
                systemInfo.Conflicts.Add(new SystemConflict
                {
                    Type = ConflictType.MultipleWebServers,
                    Severity = ConflictSeverity.Warning,
                    Title = "Multiple Web Servers Detected",
                    Description = $"Found {systemInfo.DetectedWebServers.Count} web servers installed. This may cause port conflicts or confusion.",
                    AffectedItems = systemInfo.DetectedWebServers.Select(w => w.Name).ToList()
                });
            }

            // Detect runtimes
            _logger.LogInformation("Detecting runtime environments...");
            systemInfo.Runtimes = await DetectRuntimesAsync(cancellationToken);

            // Discover websites
            _logger.LogInformation("Discovering websites...");
            systemInfo.DiscoveredSites = await _websiteDiscovery.DiscoverAllSitesAsync(cancellationToken);
            _logger.LogInformation("Found {Count} websites", systemInfo.DiscoveredSites.Count);

            // Detect environment type
            systemInfo.DetectedEnvironment = await _environmentAnalyzer.DetectEnvironmentTypeAsync(cancellationToken);
            systemInfo.EnvironmentIndicators = await GetEnvironmentIndicatorsAsync();

            // Scan system resources
            await ScanSystemResourcesAsync(systemInfo);

            // Analyze conflicts
            _logger.LogInformation("Analyzing conflicts...");
            var conflicts = await _conflictResolver.AnalyzeConflictsAsync(systemInfo, cancellationToken);
            systemInfo.Conflicts.AddRange(conflicts);

            stopwatch.Stop();
            systemInfo.ScanDuration = stopwatch.Elapsed;

            _lastScanResult = systemInfo;
            _logger.LogInformation("System scan completed in {Duration}ms. Found {Sites} sites, {Conflicts} conflicts",
                stopwatch.ElapsedMilliseconds, systemInfo.DiscoveredSites.Count, systemInfo.Conflicts.Count);

            return systemInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during system scan");
            throw;
        }
    }

    public async Task<SystemInfo> QuickScanAsync(CancellationToken cancellationToken = default)
    {
        var systemInfo = new SystemInfo
        {
            ScannedAt = DateTime.UtcNow
        };

        await ScanOsInfoAsync(systemInfo);
        systemInfo.IsRoot = IsRunningAsRoot();
        systemInfo.CurrentUserId = GetUnixUserId();
        systemInfo.CurrentUsername = Environment.UserName;
        systemInfo.WebServer = await _webServerDetector.GetPrimaryWebServerAsync(cancellationToken);
        systemInfo.DetectedEnvironment = await _environmentAnalyzer.DetectEnvironmentTypeAsync(cancellationToken);

        return systemInfo;
    }

    public SystemInfo? GetLastScanResult() => _lastScanResult;

    private async Task ScanOsInfoAsync(SystemInfo systemInfo)
    {
        systemInfo.Hostname = Environment.MachineName;
        systemInfo.Architecture = RuntimeInformation.OSArchitecture.ToString();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            systemInfo.OsName = "Linux";

            // Try to get distro info
            if (File.Exists("/etc/os-release"))
            {
                var lines = await File.ReadAllLinesAsync("/etc/os-release");
                foreach (var line in lines)
                {
                    if (line.StartsWith("PRETTY_NAME="))
                    {
                        systemInfo.OsName = line.Split('=')[1].Trim('"');
                    }
                    else if (line.StartsWith("VERSION_ID="))
                    {
                        systemInfo.OsVersion = line.Split('=')[1].Trim('"');
                    }
                }
            }

            // Get kernel version
            systemInfo.Kernel = ExecuteCommand("uname", "-r").Trim();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            systemInfo.OsName = "Windows";
            systemInfo.OsVersion = Environment.OSVersion.VersionString;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            systemInfo.OsName = "macOS";
            systemInfo.OsVersion = ExecuteCommand("sw_vers", "-productVersion").Trim();
        }

        await Task.CompletedTask;
    }

    private async Task<List<RuntimeInfo>> DetectRuntimesAsync(CancellationToken cancellationToken)
    {
        var runtimes = new List<RuntimeInfo>();

        // PHP
        var phpVersion = await TryGetVersionAsync("php", "-v");
        if (phpVersion != null)
        {
            runtimes.Add(new RuntimeInfo
            {
                Type = RuntimeType.Php,
                Version = phpVersion,
                Path = await GetCommandPathAsync("php"),
                IsDefault = true
            });
        }

        // Node.js
        var nodeVersion = await TryGetVersionAsync("node", "-v");
        if (nodeVersion != null)
        {
            runtimes.Add(new RuntimeInfo
            {
                Type = RuntimeType.NodeJs,
                Version = nodeVersion.TrimStart('v'),
                Path = await GetCommandPathAsync("node"),
                IsDefault = true
            });
        }

        // Python
        var pythonVersion = await TryGetVersionAsync("python3", "--version");
        if (pythonVersion != null)
        {
            runtimes.Add(new RuntimeInfo
            {
                Type = RuntimeType.Python,
                Version = pythonVersion.Replace("Python ", ""),
                Path = await GetCommandPathAsync("python3"),
                IsDefault = true
            });
        }

        // .NET
        var dotnetVersion = await TryGetVersionAsync("dotnet", "--version");
        if (dotnetVersion != null)
        {
            runtimes.Add(new RuntimeInfo
            {
                Type = RuntimeType.DotNet,
                Version = dotnetVersion,
                Path = await GetCommandPathAsync("dotnet"),
                IsDefault = true
            });
        }

        // Ruby
        var rubyVersion = await TryGetVersionAsync("ruby", "-v");
        if (rubyVersion != null)
        {
            runtimes.Add(new RuntimeInfo
            {
                Type = RuntimeType.Ruby,
                Version = rubyVersion,
                Path = await GetCommandPathAsync("ruby"),
                IsDefault = true
            });
        }

        // Go
        var goVersion = await TryGetVersionAsync("go", "version");
        if (goVersion != null)
        {
            runtimes.Add(new RuntimeInfo
            {
                Type = RuntimeType.Go,
                Version = goVersion,
                Path = await GetCommandPathAsync("go"),
                IsDefault = true
            });
        }

        // Java
        var javaVersion = await TryGetVersionAsync("java", "-version");
        if (javaVersion != null)
        {
            runtimes.Add(new RuntimeInfo
            {
                Type = RuntimeType.Java,
                Version = javaVersion,
                Path = await GetCommandPathAsync("java"),
                IsDefault = true
            });
        }

        return runtimes;
    }

    private async Task<string?> TryGetVersionAsync(string command, string args)
    {
        try
        {
            var result = ExecuteCommand(command, args);
            if (!string.IsNullOrWhiteSpace(result))
            {
                // Extract first line and clean up
                var firstLine = result.Split('\n')[0].Trim();
                // Extract version number pattern
                var versionMatch = System.Text.RegularExpressions.Regex.Match(firstLine, @"[\d]+\.[\d]+\.?[\d]*");
                return versionMatch.Success ? versionMatch.Value : firstLine;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get version for command: {Command}", command);
        }
        return null;
    }

    private async Task<string> GetCommandPathAsync(string command)
    {
        try
        {
            var result = ExecuteCommand("which", command);
            return result.Trim();
        }
        catch
        {
            return command;
        }
    }

    private async Task<List<string>> GetEnvironmentIndicatorsAsync()
    {
        var indicators = new List<string>();

        // Check for common environment indicators
        if (File.Exists("/.dockerenv"))
            indicators.Add("Docker container detected");

        if (Directory.Exists("/var/www"))
            indicators.Add("Standard web directory exists");

        if (File.Exists("/etc/letsencrypt"))
            indicators.Add("Let's Encrypt SSL configured");

        var hostname = Environment.MachineName.ToLower();
        if (hostname.Contains("prod"))
            indicators.Add("Hostname suggests production");
        else if (hostname.Contains("staging") || hostname.Contains("stage"))
            indicators.Add("Hostname suggests staging");
        else if (hostname.Contains("dev"))
            indicators.Add("Hostname suggests development");

        return indicators;
    }

    private async Task ScanSystemResourcesAsync(SystemInfo systemInfo)
    {
        try
        {
            // Memory info
            if (File.Exists("/proc/meminfo"))
            {
                var memInfo = await File.ReadAllTextAsync("/proc/meminfo");
                var lines = memInfo.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("MemTotal:"))
                    {
                        var kb = long.Parse(line.Split(':')[1].Trim().Split(' ')[0]);
                        systemInfo.TotalMemoryMb = kb / 1024;
                    }
                    else if (line.StartsWith("MemAvailable:"))
                    {
                        var kb = long.Parse(line.Split(':')[1].Trim().Split(' ')[0]);
                        systemInfo.AvailableMemoryMb = kb / 1024;
                    }
                }
            }

            // Disk info
            var dfResult = ExecuteCommand("df", "-BG /");
            var dfLines = dfResult.Split('\n');
            if (dfLines.Length > 1)
            {
                var parts = dfLines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    systemInfo.TotalDiskGb = long.Parse(parts[1].TrimEnd('G'));
                    systemInfo.AvailableDiskGb = long.Parse(parts[3].TrimEnd('G'));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scan system resources");
        }
    }

    private string ExecuteCommand(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return "";

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return output;
        }
        catch
        {
            return "";
        }
    }
}
