using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.Sync;

/// <summary>
/// Service for syncing projects with GitHub releases
/// </summary>
public class SyncService : ISyncService
{
    private readonly IProjectRepository _projectRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IGitHubService _gitHubService;
    private readonly IAiService _aiService;
    private readonly INotificationService _notificationService;
    private readonly IPermissionManager _permissionManager;
    private readonly ILogger<SyncService> _logger;
    private readonly AppConfig _config;

    public SyncService(
        IProjectRepository projectRepository,
        ISyncHistoryRepository syncHistoryRepository,
        IGitHubService gitHubService,
        IAiService aiService,
        INotificationService notificationService,
        IPermissionManager permissionManager,
        ILogger<SyncService> logger,
        IOptions<AppConfig> config)
    {
        _projectRepository = projectRepository;
        _syncHistoryRepository = syncHistoryRepository;
        _gitHubService = gitHubService;
        _aiService = aiService;
        _notificationService = notificationService;
        _permissionManager = permissionManager;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<SyncResult> SyncAsync(Guid projectId, SyncTrigger trigger, string? initiatedBy = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found");

        _logger.LogInformation("Starting sync for project {Name} ({Id})", project.Name, projectId);

        var result = new SyncResult();
        var history = new SyncHistory
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Trigger = trigger,
            InitiatedBy = initiatedBy ?? "system",
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Update project status
            project.Status = ProjectStatus.Syncing;
            await _projectRepository.UpdateAsync(project, cancellationToken);

            // Get latest release
            var latestRelease = await _gitHubService.GetLatestReleaseAsync(
                project.Repository,
                project.ReleaseTagPattern,
                cancellationToken);

            if (latestRelease == null)
            {
                throw new InvalidOperationException("No release found for repository");
            }

            history.Version = latestRelease.TagName;
            history.ReleaseInfo = $"{latestRelease.Name} ({latestRelease.TagName})";

            // Check if already synced
            if (project.LastSyncedVersion == latestRelease.TagName)
            {
                _logger.LogInformation("Project {Name} is already at version {Version}", project.Name, latestRelease.TagName);
                result.Success = true;
                result.Version = latestRelease.TagName;
                history.Success = true;
                history.Message = "Already at latest version";
                return result;
            }

            // Preserve original permissions
            var originalOwnership = await _permissionManager.GetOwnershipAsync(project.LocalPath, cancellationToken);
            _logger.LogDebug("Original ownership: {Owner}:{Group}", originalOwnership.Owner, originalOwnership.Group);

            // Create backup if enabled
            if (project.Config.Backup?.Enabled == true)
            {
                var backupPath = await CreateBackupAsync(project, cancellationToken);
                history.BackupPath = backupPath;
                result.BackupPath = backupPath;
                _logger.LogInformation("Backup created at {Path}", backupPath);
            }

            // Run pre-sync commands
            if (project.Config.PreSyncCommands?.Any() == true)
            {
                var preResults = await RunCommandsAsync(project.Config.PreSyncCommands, project.LocalPath, cancellationToken);
                result.CommandResults.AddRange(preResults);
                history.CommandResults.AddRange(preResults);

                if (preResults.Any(r => !r.Success))
                {
                    throw new InvalidOperationException("Pre-sync commands failed");
                }
            }

            // Download and extract release
            var downloadPath = await _gitHubService.DownloadReleaseAsync(
                project.Repository,
                latestRelease.TagName,
                Path.GetTempPath(),
                cancellationToken);

            var filesChanged = await ExtractAndSyncFilesAsync(
                downloadPath,
                project.LocalPath,
                project.Config.ExcludePaths,
                cancellationToken);

            result.FilesChanged = filesChanged;
            history.FilesChanged = filesChanged;

            // Cleanup download
            if (File.Exists(downloadPath))
            {
                File.Delete(downloadPath);
            }

            // Restore original ownership
            await _permissionManager.SetOwnershipAsync(project.LocalPath, originalOwnership.Owner, originalOwnership.Group, true, cancellationToken);

            // Run post-sync commands
            if (project.Config.PostSyncCommands?.Any() == true)
            {
                var postResults = await RunCommandsAsync(project.Config.PostSyncCommands, project.LocalPath, cancellationToken);
                result.CommandResults.AddRange(postResults);
                history.CommandResults.AddRange(postResults);

                if (postResults.Any(r => !r.Success))
                {
                    _logger.LogWarning("Some post-sync commands failed for {Name}", project.Name);
                }
            }

            // Run health check
            if (project.Config.HealthCheck?.Enabled == true)
            {
                var healthResult = await PerformHealthCheckAsync(project.Config.HealthCheck, cancellationToken);
                result.HealthCheckResult = healthResult;
                history.HealthCheckResult = healthResult;

                if (!healthResult.Success && project.Config.HealthCheck.RollbackOnFail)
                {
                    _logger.LogWarning("Health check failed, initiating rollback for {Name}", project.Name);

                    if (!string.IsNullOrEmpty(history.BackupPath))
                    {
                        await RollbackFromBackupAsync(project, history.BackupPath, cancellationToken);
                        result.RolledBack = true;
                        throw new InvalidOperationException($"Health check failed: {healthResult.ErrorMessage}. Rolled back to previous version.");
                    }
                }
            }

            // Update project
            project.LastSyncedVersion = latestRelease.TagName;
            project.LastSyncedAt = DateTime.UtcNow;
            project.Status = ProjectStatus.Synced;
            project.LastError = null;
            await _projectRepository.UpdateAsync(project, cancellationToken);

            // Record success
            result.Success = true;
            result.Version = latestRelease.TagName;
            result.HistoryId = history.Id;

            history.Success = true;
            history.Message = $"Successfully synced to {latestRelease.TagName}";

            // Send success notification
            await _notificationService.SendAsync(
                NotificationTemplates.SyncSuccess(project.Name, latestRelease.TagName, filesChanged),
                cancellationToken);

            _logger.LogInformation("Sync completed for {Name} to version {Version}", project.Name, latestRelease.TagName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for project {Name}", project.Name);

            result.Success = false;
            result.ErrorMessage = ex.Message;

            history.Success = false;
            history.Message = ex.Message;
            history.ErrorDetails = ex.ToString();

            // Update project status
            project.Status = ProjectStatus.Failed;
            project.LastError = ex.Message;
            await _projectRepository.UpdateAsync(project, cancellationToken);

            // AI error analysis if enabled
            if (_config.Ai?.Enabled == true && _config.Ai.Mode != AiMode.Off)
            {
                try
                {
                    var errorContext = new ErrorContext
                    {
                        ProjectName = project.Name,
                        Framework = project.Framework,
                        ErrorMessage = ex.Message,
                        StackTrace = ex.StackTrace
                    };

                    var analysis = await _aiService.AnalyzeErrorAsync(errorContext, cancellationToken);
                    history.AiAnalysis = analysis;
                }
                catch (Exception aiEx)
                {
                    _logger.LogWarning(aiEx, "AI analysis failed for error");
                }
            }

            // Send failure notification
            await _notificationService.SendAsync(
                NotificationTemplates.SyncFailed(project.Name, ex.Message),
                cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;
            history.CompletedAt = DateTime.UtcNow;
            history.DurationMs = stopwatch.ElapsedMilliseconds;

            await _syncHistoryRepository.CreateAsync(history, cancellationToken);
        }

        return result;
    }

    public async Task<SyncPreview> PreviewSyncAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found");

        var latestRelease = await _gitHubService.GetLatestReleaseAsync(
            project.Repository,
            project.ReleaseTagPattern,
            cancellationToken);

        if (latestRelease == null)
        {
            throw new InvalidOperationException("No release found");
        }

        var preview = new SyncPreview
        {
            CurrentVersion = project.LastSyncedVersion ?? "none",
            NewVersion = latestRelease.TagName,
            CommandsToRun = []
        };

        // Add pre-sync commands
        if (project.Config.PreSyncCommands?.Any() == true)
        {
            preview.CommandsToRun.AddRange(project.Config.PreSyncCommands);
        }

        // Add post-sync commands
        if (project.Config.PostSyncCommands?.Any() == true)
        {
            preview.CommandsToRun.AddRange(project.Config.PostSyncCommands);
        }

        // Download and analyze files (without extracting)
        var downloadPath = await _gitHubService.DownloadReleaseAsync(
            project.Repository,
            latestRelease.TagName,
            Path.GetTempPath(),
            cancellationToken);

        try
        {
            using var archive = ZipFile.OpenRead(downloadPath);
            long totalSize = 0;

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue; // Skip directories

                var relativePath = GetRelativePath(entry.FullName);
                var localPath = Path.Combine(project.LocalPath, relativePath);

                if (File.Exists(localPath))
                {
                    var localInfo = new FileInfo(localPath);
                    if (localInfo.Length != entry.Length)
                    {
                        preview.FileChanges.Add(new FileChange
                        {
                            Path = relativePath,
                            ChangeType = FileChangeType.Modified,
                            OldSize = localInfo.Length,
                            NewSize = entry.Length
                        });
                    }
                }
                else
                {
                    preview.FileChanges.Add(new FileChange
                    {
                        Path = relativePath,
                        ChangeType = FileChangeType.Added,
                        NewSize = entry.Length
                    });
                }

                totalSize += entry.Length;
            }

            preview.TotalSizeBytes = totalSize;
        }
        finally
        {
            if (File.Exists(downloadPath))
            {
                File.Delete(downloadPath);
            }
        }

        return preview;
    }

    public async Task<SyncResult> RollbackAsync(Guid projectId, Guid historyId, string? initiatedBy = null, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found");

        var history = await _syncHistoryRepository.GetByIdAsync(historyId, cancellationToken)
            ?? throw new InvalidOperationException($"History {historyId} not found");

        if (string.IsNullOrEmpty(history.BackupPath) || !Directory.Exists(history.BackupPath))
        {
            throw new InvalidOperationException("No backup available for this sync");
        }

        _logger.LogInformation("Rolling back project {Name} to version {Version}", project.Name, history.Version);

        var result = new SyncResult();

        try
        {
            await RollbackFromBackupAsync(project, history.BackupPath, cancellationToken);

            result.Success = true;
            result.Version = history.Version;
            result.RolledBack = true;

            // Update project
            project.LastSyncedVersion = history.Version;
            project.Status = ProjectStatus.Synced;
            project.LastError = null;
            await _projectRepository.UpdateAsync(project, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed for project {Name}", project.Name);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<SyncResult> RollbackToLastBackupAsync(Guid projectId, string? initiatedBy = null, CancellationToken cancellationToken = default)
    {
        var options = await GetRollbackOptionsAsync(projectId, cancellationToken);
        var lastBackup = options.FirstOrDefault(o => o.HasBackup);

        if (lastBackup == null)
        {
            throw new InvalidOperationException("No backup available");
        }

        return await RollbackAsync(projectId, lastBackup.HistoryId, initiatedBy, cancellationToken);
    }

    public async Task<IEnumerable<RollbackOption>> GetRollbackOptionsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var histories = await _syncHistoryRepository.GetByProjectAsync(projectId, 20, cancellationToken);

        return histories
            .Where(h => h.Success)
            .Select(h => new RollbackOption
            {
                HistoryId = h.Id,
                Version = h.Version ?? "unknown",
                SyncedAt = h.StartedAt,
                BackupPath = h.BackupPath,
                HasBackup = !string.IsNullOrEmpty(h.BackupPath) && Directory.Exists(h.BackupPath)
            })
            .ToList();
    }

    public async Task<bool> NeedsSyncAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(projectId, cancellationToken);
        return status.NeedsSync;
    }

    public async Task<SyncStatus> GetStatusAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException($"Project {projectId} not found");

        var status = new SyncStatus
        {
            Status = project.Status,
            CurrentVersion = project.LastSyncedVersion,
            LastSyncAt = project.LastSyncedAt,
            LastError = project.LastError
        };

        try
        {
            var latestRelease = await _gitHubService.GetLatestReleaseAsync(
                project.Repository,
                project.ReleaseTagPattern,
                cancellationToken);

            if (latestRelease != null)
            {
                status.LatestVersion = latestRelease.TagName;
                status.NeedsSync = project.LastSyncedVersion != latestRelease.TagName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check latest version for {Name}", project.Name);
        }

        return status;
    }

    private async Task<string> CreateBackupAsync(Project project, CancellationToken cancellationToken)
    {
        var backupDir = project.Config.Backup?.Directory ?? Path.Combine(_config.DataPath, "backups", project.Name);
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(backupDir, $"backup_{timestamp}");

        await Task.Run(() =>
        {
            CopyDirectory(project.LocalPath, backupPath);
        }, cancellationToken);

        // Cleanup old backups
        var maxBackups = project.Config.Backup?.RetentionCount ?? 5;
        CleanupOldBackups(backupDir, maxBackups);

        return backupPath;
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, file);
            var destFile = Path.Combine(destinationPath, relativePath);
            var destDir = Path.GetDirectoryName(destFile);

            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(file, destFile, true);
        }
    }

    private static void CleanupOldBackups(string backupDir, int maxBackups)
    {
        var backups = Directory.GetDirectories(backupDir)
            .OrderByDescending(d => d)
            .Skip(maxBackups)
            .ToList();

        foreach (var backup in backups)
        {
            try
            {
                Directory.Delete(backup, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private async Task RollbackFromBackupAsync(Project project, string backupPath, CancellationToken cancellationToken)
    {
        // Preserve original permissions
        var ownership = await _permissionManager.GetOwnershipAsync(project.LocalPath, cancellationToken);

        // Clear target directory (except excluded paths)
        var excludePaths = project.Config.ExcludePaths ?? [];
        foreach (var file in Directory.GetFiles(project.LocalPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(project.LocalPath, file);
            if (!excludePaths.Any(e => relativePath.StartsWith(e, StringComparison.OrdinalIgnoreCase)))
            {
                File.Delete(file);
            }
        }

        // Copy backup files
        await Task.Run(() =>
        {
            CopyDirectory(backupPath, project.LocalPath);
        }, cancellationToken);

        // Restore permissions
        await _permissionManager.SetOwnershipAsync(project.LocalPath, ownership.Owner, ownership.Group, true, cancellationToken);
    }

    private async Task<int> ExtractAndSyncFilesAsync(string zipPath, string destinationPath, List<string>? excludePaths, CancellationToken cancellationToken)
    {
        var filesChanged = 0;
        excludePaths ??= [];

        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipPath);

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue; // Skip directories

                var relativePath = GetRelativePath(entry.FullName);

                // Check exclusions
                if (excludePaths.Any(e => relativePath.StartsWith(e, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var destinationFile = Path.Combine(destinationPath, relativePath);
                var destinationDir = Path.GetDirectoryName(destinationFile);

                if (!string.IsNullOrEmpty(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                entry.ExtractToFile(destinationFile, true);
                filesChanged++;
            }
        }, cancellationToken);

        return filesChanged;
    }

    private static string GetRelativePath(string entryPath)
    {
        // GitHub zipball has a root folder like "owner-repo-hash/"
        var parts = entryPath.Split('/', 2);
        return parts.Length > 1 ? parts[1] : entryPath;
    }

    private async Task<List<CommandResult>> RunCommandsAsync(List<string> commands, string workingDirectory, CancellationToken cancellationToken)
    {
        var results = new List<CommandResult>();

        foreach (var command in commands)
        {
            var result = await RunCommandAsync(command, workingDirectory, cancellationToken);
            results.Add(result);

            if (!result.Success)
            {
                _logger.LogWarning("Command failed: {Command} - {Error}", command, result.ErrorOutput);
            }
        }

        return results;
    }

    private async Task<CommandResult> RunCommandAsync(string command, string workingDirectory, CancellationToken cancellationToken)
    {
        var result = new CommandResult { Command = command };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            result.Output = await outputTask;
            result.ErrorOutput = await errorTask;
            result.ExitCode = process.ExitCode;
            result.Success = process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorOutput = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    private async Task<HealthCheckResult> PerformHealthCheckAsync(HealthCheckConfig config, CancellationToken cancellationToken)
    {
        var result = new HealthCheckResult
        {
            Url = config.Url,
            ExpectedStatusCode = config.ExpectedStatusCode
        };

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds) };
            var response = await client.GetAsync(config.Url, cancellationToken);

            result.ActualStatusCode = (int)response.StatusCode;
            result.ResponseTimeMs = 0; // Would need stopwatch for accurate timing
            result.Success = (int)response.StatusCode == config.ExpectedStatusCode;

            if (!result.Success)
            {
                result.ErrorMessage = $"Expected status {config.ExpectedStatusCode} but got {(int)response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}
