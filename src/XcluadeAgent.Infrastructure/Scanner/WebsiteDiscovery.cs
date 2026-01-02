using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.Scanner;

public class WebsiteDiscovery : IWebsiteDiscovery
{
    private readonly IWebServerDetector _webServerDetector;
    private readonly IEnvironmentAnalyzer _environmentAnalyzer;
    private readonly ILogger<WebsiteDiscovery> _logger;

    public WebsiteDiscovery(
        IWebServerDetector webServerDetector,
        IEnvironmentAnalyzer environmentAnalyzer,
        ILogger<WebsiteDiscovery> logger)
    {
        _webServerDetector = webServerDetector;
        _environmentAnalyzer = environmentAnalyzer;
        _logger = logger;
    }

    public async Task<List<DiscoveredSite>> DiscoverAllSitesAsync(CancellationToken cancellationToken = default)
    {
        var allSites = new List<DiscoveredSite>();
        var webServers = await _webServerDetector.DetectWebServersAsync(cancellationToken);

        foreach (var webServer in webServers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var sites = await DiscoverSitesAsync(webServer, cancellationToken);
                allSites.AddRange(sites);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to discover sites for {WebServer}", webServer.Name);
            }
        }

        // Remove duplicates based on document root
        return allSites
            .GroupBy(s => s.DocumentRoot)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<List<DiscoveredSite>> DiscoverSitesAsync(WebServerInfo webServer, CancellationToken cancellationToken = default)
    {
        return webServer.Type switch
        {
            WebServerType.Nginx => await DiscoverNginxSitesAsync(webServer, cancellationToken),
            WebServerType.Apache => await DiscoverApacheSitesAsync(webServer, cancellationToken),
            WebServerType.Caddy => await DiscoverCaddySitesAsync(webServer, cancellationToken),
            _ => new List<DiscoveredSite>()
        };
    }

    private async Task<List<DiscoveredSite>> DiscoverNginxSitesAsync(WebServerInfo webServer, CancellationToken cancellationToken)
    {
        var sites = new List<DiscoveredSite>();

        // Check sites-enabled and conf.d directories
        var sitePaths = new[]
        {
            "/etc/nginx/sites-enabled",
            "/etc/nginx/conf.d",
            webServer.SitesPath
        }.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p)).Distinct();

        foreach (var sitePath in sitePaths)
        {
            foreach (var configFile in Directory.GetFiles(sitePath, "*.conf", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(sitePath).Where(f => !f.EndsWith(".conf") && !Path.GetFileName(f).StartsWith("."))))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var content = await File.ReadAllTextAsync(configFile, cancellationToken);
                    var parsedSites = ParseNginxConfig(content, configFile, webServer);
                    sites.AddRange(parsedSites);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to parse nginx config: {File}", configFile);
                }
            }
        }

        // Analyze each site
        foreach (var site in sites)
        {
            await EnrichSiteInfoAsync(site, cancellationToken);
        }

        return sites;
    }

    private List<DiscoveredSite> ParseNginxConfig(string content, string configFile, WebServerInfo webServer)
    {
        var sites = new List<DiscoveredSite>();

        // Find server blocks
        var serverBlocks = Regex.Matches(content, @"server\s*\{([^{}]*(?:\{[^{}]*\}[^{}]*)*)\}", RegexOptions.Singleline);

        foreach (Match serverBlock in serverBlocks)
        {
            var block = serverBlock.Groups[1].Value;

            // Skip if it's just a redirect or doesn't have a root
            if (!block.Contains("root") && !block.Contains("proxy_pass"))
                continue;

            var site = new DiscoveredSite
            {
                ConfigFile = configFile,
                WebServer = webServer.Type
            };

            // Parse server_name
            var serverNameMatch = Regex.Match(block, @"server_name\s+([^;]+);");
            if (serverNameMatch.Success)
            {
                var names = serverNameMatch.Groups[1].Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                site.Domain = names.FirstOrDefault(n => n != "_" && !n.StartsWith("www.")) ?? names.First();
                site.Aliases = names.Where(n => n != site.Domain && n != "_").ToList();
                site.Name = site.Domain.Replace(".", "-");
            }

            // Parse root
            var rootMatch = Regex.Match(block, @"root\s+([^;]+);");
            if (rootMatch.Success)
            {
                site.DocumentRoot = rootMatch.Groups[1].Value.Trim();
            }

            // Check for SSL
            site.HasSsl = block.Contains("ssl_certificate") || block.Contains("listen 443");
            if (site.HasSsl)
            {
                var sslCertMatch = Regex.Match(block, @"ssl_certificate\s+([^;]+);");
                if (sslCertMatch.Success)
                {
                    site.SslCertPath = sslCertMatch.Groups[1].Value.Trim();
                }
            }

            // Check if enabled (not commented out and in sites-enabled)
            site.IsEnabled = configFile.Contains("sites-enabled") || !Path.GetFileName(configFile).StartsWith(".");

            if (!string.IsNullOrEmpty(site.DocumentRoot) && !string.IsNullOrEmpty(site.Domain))
            {
                sites.Add(site);
            }
        }

        return sites;
    }

    private async Task<List<DiscoveredSite>> DiscoverApacheSitesAsync(WebServerInfo webServer, CancellationToken cancellationToken)
    {
        var sites = new List<DiscoveredSite>();

        var sitePaths = new[]
        {
            "/etc/apache2/sites-enabled",
            "/etc/httpd/conf.d",
            webServer.SitesPath
        }.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p)).Distinct();

        foreach (var sitePath in sitePaths)
        {
            foreach (var configFile in Directory.GetFiles(sitePath, "*.conf", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var content = await File.ReadAllTextAsync(configFile, cancellationToken);
                    var parsedSites = ParseApacheConfig(content, configFile, webServer);
                    sites.AddRange(parsedSites);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to parse apache config: {File}", configFile);
                }
            }
        }

        foreach (var site in sites)
        {
            await EnrichSiteInfoAsync(site, cancellationToken);
        }

        return sites;
    }

    private List<DiscoveredSite> ParseApacheConfig(string content, string configFile, WebServerInfo webServer)
    {
        var sites = new List<DiscoveredSite>();

        // Find VirtualHost blocks
        var vhostBlocks = Regex.Matches(content, @"<VirtualHost[^>]*>(.*?)</VirtualHost>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match vhostBlock in vhostBlocks)
        {
            var block = vhostBlock.Groups[1].Value;

            var site = new DiscoveredSite
            {
                ConfigFile = configFile,
                WebServer = webServer.Type
            };

            // Parse ServerName
            var serverNameMatch = Regex.Match(block, @"ServerName\s+(\S+)", RegexOptions.IgnoreCase);
            if (serverNameMatch.Success)
            {
                site.Domain = serverNameMatch.Groups[1].Value.Trim();
                site.Name = site.Domain.Replace(".", "-");
            }

            // Parse ServerAlias
            var aliasMatch = Regex.Match(block, @"ServerAlias\s+(.+)", RegexOptions.IgnoreCase);
            if (aliasMatch.Success)
            {
                site.Aliases = aliasMatch.Groups[1].Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            // Parse DocumentRoot
            var rootMatch = Regex.Match(block, @"DocumentRoot\s+""?([^""\s]+)""?", RegexOptions.IgnoreCase);
            if (rootMatch.Success)
            {
                site.DocumentRoot = rootMatch.Groups[1].Value.Trim();
            }

            // Check for SSL
            site.HasSsl = vhostBlock.Value.Contains(":443") || block.Contains("SSLEngine", StringComparison.OrdinalIgnoreCase);

            site.IsEnabled = configFile.Contains("sites-enabled");

            if (!string.IsNullOrEmpty(site.DocumentRoot) && !string.IsNullOrEmpty(site.Domain))
            {
                sites.Add(site);
            }
        }

        return sites;
    }

    private async Task<List<DiscoveredSite>> DiscoverCaddySitesAsync(WebServerInfo webServer, CancellationToken cancellationToken)
    {
        var sites = new List<DiscoveredSite>();

        var caddyFiles = new[]
        {
            "/etc/caddy/Caddyfile",
            webServer.ConfigPath
        }.Where(File.Exists).Distinct();

        foreach (var caddyFile in caddyFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(caddyFile, cancellationToken);
                // Parse Caddyfile format (simplified)
                var blocks = Regex.Matches(content, @"(\S+)\s*\{([^}]*)\}");

                foreach (Match block in blocks)
                {
                    var domain = block.Groups[1].Value;
                    var config = block.Groups[2].Value;

                    var site = new DiscoveredSite
                    {
                        Domain = domain,
                        Name = domain.Replace(".", "-"),
                        ConfigFile = caddyFile,
                        WebServer = WebServerType.Caddy,
                        HasSsl = true, // Caddy auto-SSL
                        IsEnabled = true
                    };

                    var rootMatch = Regex.Match(config, @"root\s+\*?\s*(\S+)");
                    if (rootMatch.Success)
                    {
                        site.DocumentRoot = rootMatch.Groups[1].Value;
                    }

                    if (!string.IsNullOrEmpty(site.DocumentRoot))
                    {
                        sites.Add(site);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse Caddyfile: {File}", caddyFile);
            }
        }

        foreach (var site in sites)
        {
            await EnrichSiteInfoAsync(site, cancellationToken);
        }

        return sites;
    }

    private async Task EnrichSiteInfoAsync(DiscoveredSite site, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(site.DocumentRoot) || !Directory.Exists(site.DocumentRoot))
            return;

        // Get ownership
        try
        {
            var fileInfo = new FileInfo(site.DocumentRoot);
            // On Unix, we need to use native calls or parse ls output
            var lsOutput = ExecuteCommand("ls", $"-ld \"{site.DocumentRoot}\"");
            var parts = lsOutput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
            {
                site.OwnerUser = parts[2];
                site.OwnerGroup = parts[3];
            }

            // Get numeric permissions
            var statOutput = ExecuteCommand("stat", $"-c %a \"{site.DocumentRoot}\"");
            if (int.TryParse(statOutput.Trim(), out var mode))
            {
                site.DirectoryPermissions = mode;
            }
        }
        catch { }

        // Detect framework
        site.DetectedFramework = await DetectFrameworkAsync(site.DocumentRoot);

        // Detect environment
        site.Environment = await _environmentAnalyzer.DetectSiteEnvironmentAsync(site.DocumentRoot, cancellationToken);
        site.EnvironmentIndicators = await _environmentAnalyzer.GetEnvironmentIndicatorsAsync(site.DocumentRoot, cancellationToken);

        // Check for git repo
        var gitDir = Path.Combine(site.DocumentRoot, ".git");
        if (Directory.Exists(gitDir))
        {
            site.HasGitRepo = true;

            try
            {
                // Get remote URL
                var configPath = Path.Combine(gitDir, "config");
                if (File.Exists(configPath))
                {
                    var gitConfig = await File.ReadAllTextAsync(configPath, cancellationToken);
                    var remoteMatch = Regex.Match(gitConfig, @"url\s*=\s*(.+)");
                    if (remoteMatch.Success)
                    {
                        site.GitRemoteUrl = remoteMatch.Groups[1].Value.Trim();
                    }
                }

                // Get current branch
                var headPath = Path.Combine(gitDir, "HEAD");
                if (File.Exists(headPath))
                {
                    var head = await File.ReadAllTextAsync(headPath, cancellationToken);
                    var branchMatch = Regex.Match(head, @"ref: refs/heads/(.+)");
                    if (branchMatch.Success)
                    {
                        site.GitBranch = branchMatch.Groups[1].Value.Trim();
                    }
                }
            }
            catch { }
        }

        // Check SSL expiry
        if (site.HasSsl && !string.IsNullOrEmpty(site.SslCertPath) && File.Exists(site.SslCertPath))
        {
            try
            {
                var opensslOutput = ExecuteCommand("openssl", $"x509 -enddate -noout -in \"{site.SslCertPath}\"");
                var dateMatch = Regex.Match(opensslOutput, @"notAfter=(.+)");
                if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value, out var expiryDate))
                {
                    site.SslExpiresAt = expiryDate;
                }
            }
            catch { }
        }
    }

    private async Task<FrameworkType> DetectFrameworkAsync(string documentRoot)
    {
        // Laravel
        if (File.Exists(Path.Combine(documentRoot, "artisan")) ||
            File.Exists(Path.Combine(documentRoot, "composer.json")))
        {
            var composerPath = Path.Combine(documentRoot, "composer.json");
            if (File.Exists(composerPath))
            {
                var content = await File.ReadAllTextAsync(composerPath);
                if (content.Contains("laravel/framework"))
                    return FrameworkType.Laravel;
            }
        }

        // Node.js / React / Vue
        var packageJsonPath = Path.Combine(documentRoot, "package.json");
        if (File.Exists(packageJsonPath))
        {
            var content = await File.ReadAllTextAsync(packageJsonPath);
            if (content.Contains("\"react\"") || content.Contains("\"react-dom\""))
                return FrameworkType.React;
            if (content.Contains("\"vue\""))
                return FrameworkType.Vue;
            if (content.Contains("\"next\""))
                return FrameworkType.React;
            if (content.Contains("\"nuxt\""))
                return FrameworkType.Vue;
            return FrameworkType.NodeJs;
        }

        // Django
        if (File.Exists(Path.Combine(documentRoot, "manage.py")) ||
            File.Exists(Path.Combine(documentRoot, "requirements.txt")))
        {
            var reqPath = Path.Combine(documentRoot, "requirements.txt");
            if (File.Exists(reqPath))
            {
                var content = await File.ReadAllTextAsync(reqPath);
                if (content.Contains("django", StringComparison.OrdinalIgnoreCase))
                    return FrameworkType.Django;
            }
        }

        // .NET
        if (Directory.GetFiles(documentRoot, "*.csproj").Any() ||
            Directory.GetFiles(documentRoot, "*.sln").Any())
            return FrameworkType.DotNet;

        // WordPress (special case)
        if (File.Exists(Path.Combine(documentRoot, "wp-config.php")) ||
            File.Exists(Path.Combine(documentRoot, "wp-content/index.php")))
            return FrameworkType.Other; // Could add WordPress enum

        // Static
        if (File.Exists(Path.Combine(documentRoot, "index.html")) &&
            !File.Exists(Path.Combine(documentRoot, "package.json")))
            return FrameworkType.Static;

        return FrameworkType.Unknown;
    }

    public async Task<DiscoveredSite> AnalyzeSiteAsync(string documentRoot, CancellationToken cancellationToken = default)
    {
        var site = new DiscoveredSite
        {
            DocumentRoot = documentRoot,
            Name = Path.GetFileName(documentRoot)
        };

        await EnrichSiteInfoAsync(site, cancellationToken);

        return site;
    }

    public async Task<List<SiteRepoMatch>> MatchSitesWithReposAsync(List<DiscoveredSite> sites, List<string> repositories, CancellationToken cancellationToken = default)
    {
        var matches = new List<SiteRepoMatch>();

        foreach (var site in sites)
        {
            var match = new SiteRepoMatch { Site = site };

            // Method 1: Git remote URL
            if (site.HasGitRepo && !string.IsNullOrEmpty(site.GitRemoteUrl))
            {
                var repoMatch = FindMatchingRepo(site.GitRemoteUrl, repositories);
                if (repoMatch != null)
                {
                    match.MatchedRepository = repoMatch;
                    match.Confidence = 0.99;
                    match.Method = MatchMethod.GitRemote;
                    match.Reason = "Matched via .git remote URL";
                    matches.Add(match);
                    continue;
                }
            }

            // Method 2: Directory name
            var dirName = Path.GetFileName(site.DocumentRoot)?.ToLower();
            if (!string.IsNullOrEmpty(dirName))
            {
                var repoMatch = repositories.FirstOrDefault(r =>
                    r.Split('/').Last().Equals(dirName, StringComparison.OrdinalIgnoreCase));
                if (repoMatch != null)
                {
                    match.MatchedRepository = repoMatch;
                    match.Confidence = 0.7;
                    match.Method = MatchMethod.DirectoryName;
                    match.Reason = "Matched via directory name";
                    matches.Add(match);
                    continue;
                }
            }

            // Method 3: package.json name
            var packageJsonPath = Path.Combine(site.DocumentRoot, "package.json");
            if (File.Exists(packageJsonPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(packageJsonPath, cancellationToken);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("name", out var nameElement))
                    {
                        var packageName = nameElement.GetString()?.ToLower();
                        if (!string.IsNullOrEmpty(packageName))
                        {
                            var repoMatch = repositories.FirstOrDefault(r =>
                                r.Split('/').Last().Equals(packageName, StringComparison.OrdinalIgnoreCase));
                            if (repoMatch != null)
                            {
                                match.MatchedRepository = repoMatch;
                                match.Confidence = 0.8;
                                match.Method = MatchMethod.PackageJson;
                                match.Reason = "Matched via package.json name";
                                matches.Add(match);
                                continue;
                            }
                        }
                    }
                }
                catch { }
            }

            // No match found
            match.Method = MatchMethod.None;
            match.Confidence = 0;
            matches.Add(match);
        }

        return matches;
    }

    private string? FindMatchingRepo(string gitUrl, List<string> repositories)
    {
        // Normalize git URL to owner/repo format
        var normalized = gitUrl
            .Replace("git@github.com:", "")
            .Replace("https://github.com/", "")
            .Replace(".git", "")
            .Trim('/');

        return repositories.FirstOrDefault(r =>
            r.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            r.Split('/').Last().Equals(normalized.Split('/').Last(), StringComparison.OrdinalIgnoreCase));
    }

    private string ExecuteCommand(string command, string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
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
