using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XcluadeAgent.Core.Interfaces;

namespace XcluadeAgent.Infrastructure.Updates;

/// <summary>
/// Service for self-updating XcluadeAgent from GitHub releases
/// </summary>
public class SelfUpdateService : ISelfUpdateService
{
    private readonly ILogger<SelfUpdateService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _dataPath;
    private readonly string _updateHistoryPath;

    private const string GitHubApiBase = "https://api.github.com";
    private const string DefaultOwner = "xjanova";
    private const string DefaultRepo = "xcluadeagent";

    public SelfUpdateService(
        ILogger<SelfUpdateService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient("GitHub");

        _dataPath = _configuration.GetValue<string>("Database:DataPath") ?? "data";
        _updateHistoryPath = Path.Combine(_dataPath, "update-history.json");

        // Ensure data directory exists
        Directory.CreateDirectory(_dataPath);
    }

    public string GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString(3) ?? "1.0.0";
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var result = new UpdateCheckResult
        {
            CurrentVersion = GetCurrentVersion()
        };

        try
        {
            var owner = _configuration.GetValue<string>("Update:GitHubOwner") ?? DefaultOwner;
            var repo = _configuration.GetValue<string>("Update:GitHubRepo") ?? DefaultRepo;
            var token = _configuration.GetValue<string>("GitHub:Token");

            // Setup request
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{GitHubApiBase}/repos/{owner}/{repo}/releases/latest");
            request.Headers.Add("User-Agent", "XcluadeAgent-Updater");
            request.Headers.Add("Accept", "application/vnd.github.v3+json");

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    result.ErrorMessage = "No releases found";
                    return result;
                }

                result.ErrorMessage = $"GitHub API error: {response.StatusCode}";
                return result;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = JsonSerializer.Deserialize<GitHubRelease>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (release == null)
            {
                result.ErrorMessage = "Invalid release data";
                return result;
            }

            // Parse version (remove 'v' prefix if present)
            var latestVersion = release.TagName?.TrimStart('v') ?? "0.0.0";
            result.LatestVersion = latestVersion;
            result.ReleaseNotes = release.Body;
            result.PublishedAt = release.PublishedAt;

            // Find the appropriate asset (prefer linux-x64 tar.gz or zip)
            var assets = release.Assets ?? new List<GitHubAsset>();
            result.Assets = assets.Select(a => new ReleaseAsset
            {
                Name = a.Name ?? "",
                DownloadUrl = a.BrowserDownloadUrl ?? "",
                Size = a.Size,
                ContentType = a.ContentType ?? ""
            }).ToList();

            // Find best matching asset
            var preferredAsset = assets.FirstOrDefault(a =>
                a.Name?.Contains("linux", StringComparison.OrdinalIgnoreCase) == true &&
                (a.Name.EndsWith(".tar.gz") || a.Name.EndsWith(".zip")))
                ?? assets.FirstOrDefault(a => a.Name?.EndsWith(".tar.gz") == true || a.Name?.EndsWith(".zip") == true)
                ?? assets.FirstOrDefault();

            if (preferredAsset != null)
            {
                result.DownloadUrl = preferredAsset.BrowserDownloadUrl;
                result.DownloadSize = preferredAsset.Size;
            }

            // Compare versions
            result.UpdateAvailable = IsNewerVersion(result.CurrentVersion, latestVersion);

            _logger.LogInformation("Update check: Current={Current}, Latest={Latest}, UpdateAvailable={Available}",
                result.CurrentVersion, latestVersion, result.UpdateAvailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            result.ErrorMessage = $"Failed to check for updates: {ex.Message}";
        }

        return result;
    }

    public async Task<UpdateResult> ApplyUpdateAsync(string version, CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersion();
        _logger.LogInformation("Starting update from {Current} to {Target}", currentVersion, version);

        try
        {
            // Step 1: Check for update and get download URL
            var checkResult = await CheckForUpdateAsync(cancellationToken);
            if (!checkResult.UpdateAvailable)
            {
                return UpdateResult.Failed("No update available");
            }

            if (string.IsNullOrEmpty(checkResult.DownloadUrl))
            {
                return UpdateResult.Failed("No download URL found for this release");
            }

            // Step 2: Create backup
            var backupPath = await CreateBackupAsync(cancellationToken);
            _logger.LogInformation("Backup created at {BackupPath}", backupPath);

            // Step 3: Download update
            var downloadPath = await DownloadUpdateAsync(checkResult.DownloadUrl, cancellationToken);
            _logger.LogInformation("Update downloaded to {DownloadPath}", downloadPath);

            // Step 4: Extract and apply
            var installPath = GetInstallPath();
            await ExtractAndApplyUpdateAsync(downloadPath, installPath, cancellationToken);
            _logger.LogInformation("Update extracted to {InstallPath}", installPath);

            // Step 5: Record history
            await RecordUpdateHistoryAsync(currentVersion, version, true, null, backupPath);

            // Step 6: Create restart marker
            await CreateRestartMarkerAsync(version);

            _logger.LogInformation("Update to version {Version} completed successfully", version);

            return UpdateResult.Succeeded(version, backupPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply update to version {Version}", version);
            await RecordUpdateHistoryAsync(currentVersion, version, false, ex.Message, null);
            return UpdateResult.Failed($"Update failed: {ex.Message}");
        }
    }

    public async Task<List<UpdateHistoryEntry>> GetUpdateHistoryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_updateHistoryPath))
            {
                return new List<UpdateHistoryEntry>();
            }

            var json = await File.ReadAllTextAsync(_updateHistoryPath, cancellationToken);
            return JsonSerializer.Deserialize<List<UpdateHistoryEntry>>(json) ?? new List<UpdateHistoryEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read update history");
            return new List<UpdateHistoryEntry>();
        }
    }

    #region Private Methods

    private bool IsNewerVersion(string current, string latest)
    {
        try
        {
            var currentParts = current.Split('.').Select(int.Parse).ToArray();
            var latestParts = latest.Split('.').Select(int.Parse).ToArray();

            // Pad arrays to same length
            var maxLength = Math.Max(currentParts.Length, latestParts.Length);
            Array.Resize(ref currentParts, maxLength);
            Array.Resize(ref latestParts, maxLength);

            for (int i = 0; i < maxLength; i++)
            {
                if (latestParts[i] > currentParts[i]) return true;
                if (latestParts[i] < currentParts[i]) return false;
            }

            return false;
        }
        catch
        {
            // If parsing fails, do string comparison
            return string.Compare(latest, current, StringComparison.Ordinal) > 0;
        }
    }

    private string GetInstallPath()
    {
        // Get the directory where the application is running
        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(exePath))
        {
            return Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        }
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private async Task<string> CreateBackupAsync(CancellationToken cancellationToken)
    {
        var installPath = GetInstallPath();
        var backupDir = Path.Combine(_dataPath, "backups");
        Directory.CreateDirectory(backupDir);

        var backupName = $"backup-{GetCurrentVersion()}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        var backupPath = Path.Combine(backupDir, backupName);

        // Copy current installation to backup
        await Task.Run(() =>
        {
            CopyDirectory(installPath, backupPath, new[] { "data", "logs", "backups" });
        }, cancellationToken);

        // Keep only last 5 backups
        CleanupOldBackups(backupDir, 5);

        return backupPath;
    }

    private void CopyDirectory(string source, string destination, string[] excludeDirs)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            var dirName = Path.GetFileName(dir);
            if (excludeDirs.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                continue;

            var destDir = Path.Combine(destination, dirName);
            CopyDirectory(dir, destDir, excludeDirs);
        }
    }

    private void CleanupOldBackups(string backupDir, int keepCount)
    {
        try
        {
            var backups = Directory.GetDirectories(backupDir)
                .OrderByDescending(d => Directory.GetCreationTime(d))
                .Skip(keepCount)
                .ToList();

            foreach (var backup in backups)
            {
                Directory.Delete(backup, true);
                _logger.LogInformation("Deleted old backup: {Backup}", backup);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old backups");
        }
    }

    private async Task<string> DownloadUpdateAsync(string url, CancellationToken cancellationToken)
    {
        var token = _configuration.GetValue<string>("GitHub:Token");
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "XcluadeAgent-Updater");
        request.Headers.Add("Accept", "application/octet-stream");

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Add("Authorization", $"Bearer {token}");
        }

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var downloadDir = Path.Combine(_dataPath, "downloads");
        Directory.CreateDirectory(downloadDir);

        var fileName = Path.GetFileName(new Uri(url).LocalPath);
        var downloadPath = Path.Combine(downloadDir, fileName);

        await using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fileStream, cancellationToken);

        return downloadPath;
    }

    private async Task ExtractAndApplyUpdateAsync(string archivePath, string installPath, CancellationToken cancellationToken)
    {
        var tempExtractPath = Path.Combine(_dataPath, "temp-extract");

        try
        {
            // Clean temp directory
            if (Directory.Exists(tempExtractPath))
            {
                Directory.Delete(tempExtractPath, true);
            }
            Directory.CreateDirectory(tempExtractPath);

            // Extract archive
            if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                await ExtractTarGzAsync(archivePath, tempExtractPath, cancellationToken);
            }
            else if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, tempExtractPath, true);
            }
            else
            {
                throw new NotSupportedException($"Unsupported archive format: {archivePath}");
            }

            // Find the actual content directory (might be nested)
            var contentDir = FindContentDirectory(tempExtractPath);

            // Create update script that will be executed after app stops
            var updateScript = CreateUpdateScript(contentDir, installPath);
            _logger.LogInformation("Update script created: {Script}", updateScript);
        }
        finally
        {
            // Cleanup downloaded archive
            try { File.Delete(archivePath); } catch { /* ignore */ }
        }
    }

    private async Task ExtractTarGzAsync(string archivePath, string extractPath, CancellationToken cancellationToken)
    {
        // Use tar command on Linux
        var startInfo = new ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"-xzf \"{archivePath}\" -C \"{extractPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process != null)
        {
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                throw new Exception($"tar extraction failed: {error}");
            }
        }
    }

    private string FindContentDirectory(string extractPath)
    {
        // If there's a single subdirectory, use that
        var dirs = Directory.GetDirectories(extractPath);
        if (dirs.Length == 1 && Directory.GetFiles(extractPath).Length == 0)
        {
            return dirs[0];
        }
        return extractPath;
    }

    private string CreateUpdateScript(string sourcePath, string targetPath)
    {
        var scriptPath = Path.Combine(_dataPath, "apply-update.sh");
        var script = $@"#!/bin/bash
# XcluadeAgent Update Script
# Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

echo ""Waiting for application to stop...""
sleep 3

# Stop the service
systemctl stop xcluadeagent 2>/dev/null || true

echo ""Copying new files...""
cp -rf ""{sourcePath}/""* ""{targetPath}/""

# Fix permissions
chmod +x ""{targetPath}/XcluadeAgent.Api"" 2>/dev/null || true

echo ""Cleaning up...""
rm -rf ""{sourcePath}""
rm -rf ""{Path.Combine(_dataPath, "temp-extract")}""

echo ""Starting service...""
systemctl start xcluadeagent 2>/dev/null || dotnet ""{targetPath}/XcluadeAgent.Api.dll"" &

echo ""Update complete!""
rm -f ""{scriptPath}""
";

        File.WriteAllText(scriptPath, script);

        // Make executable
        try
        {
            Process.Start("chmod", $"+x \"{scriptPath}\"")?.WaitForExit();
        }
        catch { /* ignore on non-Linux */ }

        return scriptPath;
    }

    private async Task CreateRestartMarkerAsync(string targetVersion)
    {
        var markerPath = Path.Combine(_dataPath, "pending-update.json");
        var marker = new
        {
            TargetVersion = targetVersion,
            CreatedAt = DateTime.UtcNow,
            ScriptPath = Path.Combine(_dataPath, "apply-update.sh")
        };

        await File.WriteAllTextAsync(markerPath, JsonSerializer.Serialize(marker));
    }

    private async Task RecordUpdateHistoryAsync(string fromVersion, string toVersion, bool success, string? error, string? backupPath)
    {
        var history = await GetUpdateHistoryAsync();
        history.Insert(0, new UpdateHistoryEntry
        {
            FromVersion = fromVersion,
            ToVersion = toVersion,
            UpdatedAt = DateTime.UtcNow,
            Success = success,
            ErrorMessage = error,
            BackupPath = backupPath
        });

        // Keep only last 50 entries
        if (history.Count > 50)
        {
            history = history.Take(50).ToList();
        }

        var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_updateHistoryPath, json);
    }

    #endregion

    #region GitHub DTOs

    private class GitHubRelease
    {
        public string? TagName { get; set; }
        public string? Name { get; set; }
        public string? Body { get; set; }
        public DateTime? PublishedAt { get; set; }
        public List<GitHubAsset>? Assets { get; set; }
    }

    private class GitHubAsset
    {
        public string? Name { get; set; }
        public string? BrowserDownloadUrl { get; set; }
        public long Size { get; set; }
        public string? ContentType { get; set; }
    }

    #endregion
}
