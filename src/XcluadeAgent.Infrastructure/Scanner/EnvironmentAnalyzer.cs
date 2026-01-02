using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.Scanner;

/// <summary>
/// Analyzes system environment to determine type (production, staging, local, etc.)
/// </summary>
public class EnvironmentAnalyzer : IEnvironmentAnalyzer
{
    private readonly ILogger<EnvironmentAnalyzer> _logger;

    public EnvironmentAnalyzer(ILogger<EnvironmentAnalyzer> logger)
    {
        _logger = logger;
    }

    public async Task<EnvironmentType> DetectEnvironmentTypeAsync(CancellationToken cancellationToken = default)
    {
        var indicators = new List<(EnvironmentType type, int weight)>();

        // Check if running in container
        if (IsRunningInContainer())
        {
            indicators.Add((EnvironmentType.Container, 100));
        }

        // Check if running in VM
        if (await IsRunningInVmAsync())
        {
            indicators.Add((EnvironmentType.VirtualMachine, 50));
        }

        // Check cloud provider
        var cloudProvider = await DetectCloudProviderAsync();
        if (cloudProvider != null)
        {
            indicators.Add((EnvironmentType.Cloud, 80));
        }

        // Check hostname patterns
        var hostname = Environment.MachineName.ToLower();
        if (hostname.Contains("prod") || hostname.Contains("prd"))
            indicators.Add((EnvironmentType.Production, 60));
        else if (hostname.Contains("staging") || hostname.Contains("stg") || hostname.Contains("stage"))
            indicators.Add((EnvironmentType.Staging, 60));
        else if (hostname.Contains("dev") || hostname.Contains("develop"))
            indicators.Add((EnvironmentType.Development, 60));
        else if (hostname.Contains("local") || hostname == "localhost")
            indicators.Add((EnvironmentType.Local, 70));

        // Check environment variables
        var env = Environment.GetEnvironmentVariable("ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("NODE_ENV")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("APP_ENV")
            ?? Environment.GetEnvironmentVariable("RAILS_ENV");

        if (!string.IsNullOrEmpty(env))
        {
            var envType = env.ToLower() switch
            {
                "production" or "prod" => EnvironmentType.Production,
                "staging" or "stage" => EnvironmentType.Staging,
                "development" or "dev" => EnvironmentType.Development,
                "local" => EnvironmentType.Local,
                "test" or "testing" => EnvironmentType.Development,
                _ => EnvironmentType.Unknown
            };
            if (envType != EnvironmentType.Unknown)
            {
                indicators.Add((envType, 90));
            }
        }

        // Check for SSL certificates (production indicator)
        if (Directory.Exists("/etc/letsencrypt/live") &&
            Directory.GetDirectories("/etc/letsencrypt/live").Length > 0)
        {
            indicators.Add((EnvironmentType.Production, 40));
        }

        // Check for public IP (production/cloud indicator)
        var hasPublicIp = await HasPublicIpAsync();
        if (hasPublicIp)
        {
            indicators.Add((EnvironmentType.Production, 30));
        }

        // Calculate winner
        if (!indicators.Any())
            return EnvironmentType.Unknown;

        var grouped = indicators
            .GroupBy(i => i.type)
            .Select(g => new { Type = g.Key, TotalWeight = g.Sum(x => x.weight) })
            .OrderByDescending(g => g.TotalWeight)
            .First();

        return grouped.Type;
    }

    public async Task<SiteEnvironment> DetectSiteEnvironmentAsync(string documentRoot, CancellationToken cancellationToken = default)
    {
        var indicators = new List<(SiteEnvironment env, int weight)>();

        // Check .env file
        var envFile = Path.Combine(documentRoot, ".env");
        if (File.Exists(envFile))
        {
            try
            {
                var content = await File.ReadAllTextAsync(envFile, cancellationToken);

                // Look for APP_ENV or similar
                var envMatch = Regex.Match(content, @"(?:APP_ENV|NODE_ENV|ENVIRONMENT)\s*=\s*(\w+)", RegexOptions.IgnoreCase);
                if (envMatch.Success)
                {
                    var env = envMatch.Groups[1].Value.ToLower() switch
                    {
                        "production" or "prod" => SiteEnvironment.Production,
                        "staging" or "stage" => SiteEnvironment.Staging,
                        "development" or "dev" => SiteEnvironment.Development,
                        "testing" or "test" => SiteEnvironment.Testing,
                        "local" => SiteEnvironment.Local,
                        _ => SiteEnvironment.Unknown
                    };
                    if (env != SiteEnvironment.Unknown)
                    {
                        indicators.Add((env, 100));
                    }
                }

                // Check APP_DEBUG
                if (Regex.IsMatch(content, @"APP_DEBUG\s*=\s*true", RegexOptions.IgnoreCase))
                {
                    indicators.Add((SiteEnvironment.Development, 50));
                }
            }
            catch { }
        }

        // Check directory name
        var dirName = Path.GetFileName(documentRoot).ToLower();
        if (dirName.Contains("prod"))
            indicators.Add((SiteEnvironment.Production, 40));
        else if (dirName.Contains("staging") || dirName.Contains("stage"))
            indicators.Add((SiteEnvironment.Staging, 40));
        else if (dirName.Contains("dev"))
            indicators.Add((SiteEnvironment.Development, 40));
        else if (dirName.Contains("local"))
            indicators.Add((SiteEnvironment.Local, 40));

        // Check for staging/dev subdomains in config files
        var configFiles = new[] { "vhost.conf", "nginx.conf", ".htaccess" }
            .Select(f => Path.Combine(documentRoot, f))
            .Where(File.Exists);

        foreach (var configFile in configFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(configFile, cancellationToken);
                if (content.Contains("staging.", StringComparison.OrdinalIgnoreCase))
                    indicators.Add((SiteEnvironment.Staging, 60));
                if (content.Contains("dev.", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("development.", StringComparison.OrdinalIgnoreCase))
                    indicators.Add((SiteEnvironment.Development, 60));
            }
            catch { }
        }

        // Check for node_modules/.cache or similar dev artifacts
        if (Directory.Exists(Path.Combine(documentRoot, "node_modules/.cache")))
        {
            indicators.Add((SiteEnvironment.Development, 20));
        }

        // Check for production optimizations
        var buildDir = Path.Combine(documentRoot, "build");
        var distDir = Path.Combine(documentRoot, "dist");
        var publicBuildDir = Path.Combine(documentRoot, "public/build");
        if (Directory.Exists(buildDir) || Directory.Exists(distDir) || Directory.Exists(publicBuildDir))
        {
            // Has build artifacts - could be production
            indicators.Add((SiteEnvironment.Production, 20));
        }

        // Calculate
        if (!indicators.Any())
            return SiteEnvironment.Unknown;

        return indicators
            .GroupBy(i => i.env)
            .Select(g => new { Env = g.Key, TotalWeight = g.Sum(x => x.weight) })
            .OrderByDescending(g => g.TotalWeight)
            .First()
            .Env;
    }

    public async Task<List<string>> GetEnvironmentIndicatorsAsync(string path, CancellationToken cancellationToken = default)
    {
        var indicators = new List<string>();

        // Check .env file
        var envFile = Path.Combine(path, ".env");
        if (File.Exists(envFile))
        {
            try
            {
                var content = await File.ReadAllTextAsync(envFile, cancellationToken);

                var envMatch = Regex.Match(content, @"(?:APP_ENV|NODE_ENV)\s*=\s*(\w+)", RegexOptions.IgnoreCase);
                if (envMatch.Success)
                {
                    indicators.Add($"Environment variable: {envMatch.Groups[1].Value}");
                }

                if (Regex.IsMatch(content, @"APP_DEBUG\s*=\s*true", RegexOptions.IgnoreCase))
                {
                    indicators.Add("Debug mode: enabled");
                }

                if (Regex.IsMatch(content, @"APP_URL\s*=\s*https?://localhost", RegexOptions.IgnoreCase))
                {
                    indicators.Add("APP_URL points to localhost");
                }
            }
            catch { }
        }

        // Check for common development files
        if (File.Exists(Path.Combine(path, ".env.local")))
            indicators.Add("Local environment file present (.env.local)");

        if (File.Exists(Path.Combine(path, ".env.development")))
            indicators.Add("Development environment file present");

        // Check git branch
        var headFile = Path.Combine(path, ".git/HEAD");
        if (File.Exists(headFile))
        {
            try
            {
                var head = await File.ReadAllTextAsync(headFile, cancellationToken);
                var branchMatch = Regex.Match(head, @"ref: refs/heads/(.+)");
                if (branchMatch.Success)
                {
                    var branch = branchMatch.Groups[1].Value.Trim();
                    indicators.Add($"Git branch: {branch}");

                    if (branch == "main" || branch == "master" || branch == "production")
                        indicators.Add("Branch suggests production");
                    else if (branch.Contains("develop") || branch.Contains("dev"))
                        indicators.Add("Branch suggests development");
                    else if (branch.Contains("staging"))
                        indicators.Add("Branch suggests staging");
                }
            }
            catch { }
        }

        // Check for SSL
        var nginxConf = Path.Combine(path, "../nginx.conf");
        if (File.Exists(nginxConf) || HasSslConfig(path))
        {
            indicators.Add("SSL configuration detected");
        }

        return indicators;
    }

    public bool IsRunningInContainer()
    {
        // Check for Docker
        if (File.Exists("/.dockerenv"))
            return true;

        // Check cgroup
        if (File.Exists("/proc/1/cgroup"))
        {
            try
            {
                var content = File.ReadAllText("/proc/1/cgroup");
                if (content.Contains("docker") || content.Contains("kubepods") || content.Contains("containerd"))
                    return true;
            }
            catch { }
        }

        // Check for Kubernetes
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
            return true;

        return false;
    }

    public async Task<bool> IsRunningInVmAsync()
    {
        try
        {
            // Check systemd-detect-virt
            var result = await ExecuteCommandAsync("systemd-detect-virt", "");
            if (!string.IsNullOrEmpty(result) && result.Trim() != "none")
                return true;

            // Check DMI data
            var productName = await ExecuteCommandAsync("cat", "/sys/class/dmi/id/product_name 2>/dev/null");
            var vmIndicators = new[] { "VMware", "VirtualBox", "KVM", "QEMU", "Hyper-V", "Xen" };
            if (vmIndicators.Any(v => productName.Contains(v, StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        catch { }

        return false;
    }

    public async Task<string?> DetectCloudProviderAsync()
    {
        try
        {
            // AWS
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };

            try
            {
                var awsResponse = await httpClient.GetAsync("http://169.254.169.254/latest/meta-data/");
                if (awsResponse.IsSuccessStatusCode)
                    return "AWS";
            }
            catch { }

            // GCP
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "http://metadata.google.internal/computeMetadata/v1/");
                request.Headers.Add("Metadata-Flavor", "Google");
                var gcpResponse = await httpClient.SendAsync(request);
                if (gcpResponse.IsSuccessStatusCode)
                    return "GCP";
            }
            catch { }

            // Azure
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "http://169.254.169.254/metadata/instance?api-version=2021-02-01");
                request.Headers.Add("Metadata", "true");
                var azureResponse = await httpClient.SendAsync(request);
                if (azureResponse.IsSuccessStatusCode)
                    return "Azure";
            }
            catch { }

            // DigitalOcean
            try
            {
                var doResponse = await httpClient.GetAsync("http://169.254.169.254/metadata/v1/");
                if (doResponse.IsSuccessStatusCode)
                    return "DigitalOcean";
            }
            catch { }
        }
        catch { }

        return null;
    }

    private async Task<bool> HasPublicIpAsync()
    {
        try
        {
            var result = await ExecuteCommandAsync("ip", "route get 1.1.1.1 2>/dev/null | grep -oP 'src \\K\\S+'");
            var ip = result.Trim();
            if (!string.IsNullOrEmpty(ip))
            {
                // Check if it's a private IP
                var isPrivate = ip.StartsWith("10.") ||
                    ip.StartsWith("172.16.") || ip.StartsWith("172.17.") || ip.StartsWith("172.18.") ||
                    ip.StartsWith("172.19.") || ip.StartsWith("172.20.") || ip.StartsWith("172.21.") ||
                    ip.StartsWith("172.22.") || ip.StartsWith("172.23.") || ip.StartsWith("172.24.") ||
                    ip.StartsWith("172.25.") || ip.StartsWith("172.26.") || ip.StartsWith("172.27.") ||
                    ip.StartsWith("172.28.") || ip.StartsWith("172.29.") || ip.StartsWith("172.30.") ||
                    ip.StartsWith("172.31.") ||
                    ip.StartsWith("192.168.") ||
                    ip.StartsWith("127.");

                return !isPrivate;
            }
        }
        catch { }

        return false;
    }

    private bool HasSslConfig(string path)
    {
        try
        {
            var parentDir = Directory.GetParent(path)?.FullName;
            if (parentDir != null)
            {
                var files = Directory.GetFiles(parentDir, "*.conf", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    var content = File.ReadAllText(file);
                    if (content.Contains("ssl_certificate") || content.Contains("SSLCertificateFile"))
                        return true;
                }
            }
        }
        catch { }

        return false;
    }

    private async Task<string> ExecuteCommandAsync(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command} {args}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return "";

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }
        catch
        {
            return "";
        }
    }
}
