using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.Scanner;

public class WebServerDetector : IWebServerDetector
{
    private readonly ILogger<WebServerDetector> _logger;

    private static readonly Dictionary<WebServerType, WebServerConfig> WebServerConfigs = new()
    {
        [WebServerType.Nginx] = new WebServerConfig
        {
            ServiceNames = new[] { "nginx" },
            ConfigPaths = new[] { "/etc/nginx/nginx.conf", "/usr/local/nginx/conf/nginx.conf" },
            SitesPaths = new[] { "/etc/nginx/sites-enabled", "/etc/nginx/conf.d", "/usr/local/nginx/conf/sites-enabled" },
            LogPaths = new[] { "/var/log/nginx" },
            VersionCommand = "nginx",
            VersionArgs = "-v"
        },
        [WebServerType.Apache] = new WebServerConfig
        {
            ServiceNames = new[] { "apache2", "httpd" },
            ConfigPaths = new[] { "/etc/apache2/apache2.conf", "/etc/httpd/conf/httpd.conf" },
            SitesPaths = new[] { "/etc/apache2/sites-enabled", "/etc/httpd/conf.d" },
            LogPaths = new[] { "/var/log/apache2", "/var/log/httpd" },
            VersionCommand = "apache2",
            VersionArgs = "-v"
        },
        [WebServerType.Caddy] = new WebServerConfig
        {
            ServiceNames = new[] { "caddy" },
            ConfigPaths = new[] { "/etc/caddy/Caddyfile", "/etc/caddy/caddy.json" },
            SitesPaths = new[] { "/etc/caddy/sites" },
            LogPaths = new[] { "/var/log/caddy" },
            VersionCommand = "caddy",
            VersionArgs = "version"
        },
        [WebServerType.LiteSpeed] = new WebServerConfig
        {
            ServiceNames = new[] { "lsws", "lshttpd" },
            ConfigPaths = new[] { "/usr/local/lsws/conf/httpd_config.xml" },
            SitesPaths = new[] { "/usr/local/lsws/conf/vhosts" },
            LogPaths = new[] { "/usr/local/lsws/logs" },
            VersionCommand = "/usr/local/lsws/bin/lshttpd",
            VersionArgs = "-v"
        },
        [WebServerType.OpenLiteSpeed] = new WebServerConfig
        {
            ServiceNames = new[] { "lsws", "openlitespeed" },
            ConfigPaths = new[] { "/usr/local/lsws/conf/httpd_config.conf" },
            SitesPaths = new[] { "/usr/local/lsws/conf/vhosts" },
            LogPaths = new[] { "/usr/local/lsws/logs" },
            VersionCommand = "/usr/local/lsws/bin/openlitespeed",
            VersionArgs = "-v"
        }
    };

    public WebServerDetector(ILogger<WebServerDetector> logger)
    {
        _logger = logger;
    }

    public async Task<List<WebServerInfo>> DetectWebServersAsync(CancellationToken cancellationToken = default)
    {
        var detectedServers = new List<WebServerInfo>();

        foreach (var (type, config) in WebServerConfigs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = await DetectWebServerAsync(type, config, cancellationToken);
            if (info != null)
            {
                detectedServers.Add(info);
                _logger.LogInformation("Detected web server: {Name} {Version}", info.Name, info.Version);
            }
        }

        // Sort by running status (running first) then by type preference
        return detectedServers
            .OrderByDescending(s => s.IsRunning)
            .ThenBy(s => s.Type)
            .ToList();
    }

    public async Task<WebServerInfo?> GetPrimaryWebServerAsync(CancellationToken cancellationToken = default)
    {
        var servers = await DetectWebServersAsync(cancellationToken);

        // Priority: Running server > Enabled server > Any installed server
        return servers.FirstOrDefault(s => s.IsRunning)
            ?? servers.FirstOrDefault(s => s.IsEnabled)
            ?? servers.FirstOrDefault();
    }

    public async Task<bool> IsWebServerInstalledAsync(WebServerType type, CancellationToken cancellationToken = default)
    {
        if (!WebServerConfigs.TryGetValue(type, out var config))
            return false;

        return await Task.FromResult(config.ConfigPaths.Any(File.Exists));
    }

    public async Task<WebServerInfo?> GetWebServerInfoAsync(WebServerType type, CancellationToken cancellationToken = default)
    {
        if (!WebServerConfigs.TryGetValue(type, out var config))
            return null;

        return await DetectWebServerAsync(type, config, cancellationToken);
    }

    private async Task<WebServerInfo?> DetectWebServerAsync(WebServerType type, WebServerConfig config, CancellationToken cancellationToken)
    {
        // Check if any config file exists
        var configPath = config.ConfigPaths.FirstOrDefault(File.Exists);
        if (configPath == null)
            return null;

        var info = new WebServerInfo
        {
            Type = type,
            Name = type.ToString(),
            ConfigPath = configPath,
            SitesPath = config.SitesPaths.FirstOrDefault(Directory.Exists) ?? "",
            LogPath = config.LogPaths.FirstOrDefault(Directory.Exists) ?? ""
        };

        // Get version
        info.Version = await GetVersionAsync(config.VersionCommand, config.VersionArgs, type);

        // Check service status
        foreach (var serviceName in config.ServiceNames)
        {
            var (isRunning, isEnabled) = await CheckServiceStatusAsync(serviceName);
            if (isRunning || isEnabled)
            {
                info.ServiceName = serviceName;
                info.IsRunning = isRunning;
                info.IsEnabled = isEnabled;
                break;
            }
        }

        // Get process info if running
        if (info.IsRunning)
        {
            info.ProcessId = await GetProcessIdAsync(type);
            info.ListeningPorts = await GetListeningPortsAsync(info.ProcessId ?? 0);
            (info.RunAsUser, info.RunAsGroup) = await GetProcessOwnerAsync(type);
        }

        return info;
    }

    private async Task<string> GetVersionAsync(string command, string args, WebServerType type)
    {
        try
        {
            var output = ExecuteCommand(command, args);
            if (string.IsNullOrEmpty(output))
            {
                // Try alternative paths
                if (type == WebServerType.Apache)
                {
                    output = ExecuteCommand("httpd", args);
                }
            }

            // Extract version number
            var match = Regex.Match(output, @"[\d]+\.[\d]+\.[\d]+");
            return match.Success ? match.Value : output.Split('\n')[0].Trim();
        }
        catch
        {
            return "unknown";
        }
    }

    private async Task<(bool isRunning, bool isEnabled)> CheckServiceStatusAsync(string serviceName)
    {
        try
        {
            // Check with systemctl
            var statusOutput = ExecuteCommand("systemctl", $"is-active {serviceName}");
            var isRunning = statusOutput.Trim() == "active";

            var enabledOutput = ExecuteCommand("systemctl", $"is-enabled {serviceName}");
            var isEnabled = enabledOutput.Trim() == "enabled";

            return (isRunning, isEnabled);
        }
        catch
        {
            // Fallback: check if process is running
            try
            {
                var pidofOutput = ExecuteCommand("pidof", serviceName);
                return (!string.IsNullOrWhiteSpace(pidofOutput), false);
            }
            catch
            {
                return (false, false);
            }
        }
    }

    private async Task<int?> GetProcessIdAsync(WebServerType type)
    {
        var processName = type switch
        {
            WebServerType.Nginx => "nginx",
            WebServerType.Apache => "apache2",
            WebServerType.Caddy => "caddy",
            WebServerType.LiteSpeed or WebServerType.OpenLiteSpeed => "litespeed",
            _ => null
        };

        if (processName == null) return null;

        try
        {
            var output = ExecuteCommand("pidof", processName);
            var pids = output.Trim().Split(' ');
            if (pids.Length > 0 && int.TryParse(pids[0], out var pid))
                return pid;
        }
        catch { }

        return null;
    }

    private async Task<List<int>> GetListeningPortsAsync(int pid)
    {
        var ports = new List<int>();

        try
        {
            // Use ss or netstat to find listening ports
            var output = ExecuteCommand("ss", "-tlnp");
            var lines = output.Split('\n');

            foreach (var line in lines)
            {
                if (line.Contains($"pid={pid}") || line.Contains("nginx") || line.Contains("apache"))
                {
                    var match = Regex.Match(line, @":(\d+)\s");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var port))
                    {
                        if (!ports.Contains(port))
                            ports.Add(port);
                    }
                }
            }
        }
        catch { }

        return ports;
    }

    private async Task<(string user, string group)> GetProcessOwnerAsync(WebServerType type)
    {
        try
        {
            // Check nginx config for user directive
            if (type == WebServerType.Nginx && File.Exists("/etc/nginx/nginx.conf"))
            {
                var config = await File.ReadAllTextAsync("/etc/nginx/nginx.conf");
                var match = Regex.Match(config, @"^\s*user\s+(\w+)\s*(\w+)?", RegexOptions.Multiline);
                if (match.Success)
                {
                    var user = match.Groups[1].Value;
                    var group = match.Groups[2].Success ? match.Groups[2].Value : user;
                    return (user, group);
                }
            }

            // Check apache config
            if (type == WebServerType.Apache)
            {
                if (File.Exists("/etc/apache2/envvars"))
                {
                    var envvars = await File.ReadAllTextAsync("/etc/apache2/envvars");
                    var userMatch = Regex.Match(envvars, @"APACHE_RUN_USER=(\w+)");
                    var groupMatch = Regex.Match(envvars, @"APACHE_RUN_GROUP=(\w+)");
                    if (userMatch.Success)
                    {
                        return (userMatch.Groups[1].Value, groupMatch.Success ? groupMatch.Groups[1].Value : userMatch.Groups[1].Value);
                    }
                }
            }

            // Default common users
            return type switch
            {
                WebServerType.Nginx => ("www-data", "www-data"),
                WebServerType.Apache => ("www-data", "www-data"),
                WebServerType.Caddy => ("caddy", "caddy"),
                _ => ("nobody", "nogroup")
            };
        }
        catch
        {
            return ("www-data", "www-data");
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
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            return !string.IsNullOrEmpty(output) ? output : error;
        }
        catch
        {
            return "";
        }
    }

    private class WebServerConfig
    {
        public string[] ServiceNames { get; set; } = Array.Empty<string>();
        public string[] ConfigPaths { get; set; } = Array.Empty<string>();
        public string[] SitesPaths { get; set; } = Array.Empty<string>();
        public string[] LogPaths { get; set; } = Array.Empty<string>();
        public string VersionCommand { get; set; } = "";
        public string VersionArgs { get; set; } = "";
    }
}
