using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.Scanner;

/// <summary>
/// Smart permission management that preserves original ownership
/// while running as root for full access
/// </summary>
public class PermissionManager : IPermissionManager
{
    private readonly ILogger<PermissionManager> _logger;

    // Cache of common web server user/group combinations
    private static readonly Dictionary<string, (string user, string group)> CommonWebServerUsers = new()
    {
        { "nginx", ("www-data", "www-data") },
        { "apache2", ("www-data", "www-data") },
        { "httpd", ("apache", "apache") },
        { "caddy", ("caddy", "caddy") },
        { "litespeed", ("nobody", "nobody") }
    };

    public PermissionManager(ILogger<PermissionManager> logger)
    {
        _logger = logger;
    }

    public async Task<(string user, string group)> GetOwnershipAsync(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ("", "");
        }

        try
        {
            // Use stat command to get owner info
            var result = await ExecuteCommandAsync("stat", $"-c '%U:%G' \"{path}\"");
            var parts = result.Trim().Trim('\'').Split(':');

            if (parts.Length == 2)
            {
                return (parts[0], parts[1]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get ownership for {Path}", path);
        }

        return ("unknown", "unknown");
    }

    public async Task SetOwnershipAsync(string path, string user, string group, bool recursive = true)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _logger.LogWarning("SetOwnership is only supported on Linux/macOS");
            return;
        }

        try
        {
            var recursiveFlag = recursive ? "-R" : "";
            await ExecuteCommandAsync("chown", $"{recursiveFlag} {user}:{group} \"{path}\"");
            _logger.LogDebug("Set ownership of {Path} to {User}:{Group}", path, user, group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set ownership for {Path}", path);
            throw;
        }
    }

    public async Task<(string user, string group)> GetRecommendedOwnershipAsync(WebServerInfo webServer)
    {
        // First, try to get from web server config
        if (!string.IsNullOrEmpty(webServer.RunAsUser))
        {
            return (webServer.RunAsUser, webServer.RunAsGroup ?? webServer.RunAsUser);
        }

        // Fall back to common defaults
        if (CommonWebServerUsers.TryGetValue(webServer.ServiceName, out var ownership))
        {
            return ownership;
        }

        // Check if common users exist
        var usersToCheck = new[] { "www-data", "nginx", "apache", "http", "caddy" };
        foreach (var user in usersToCheck)
        {
            if (await UserExistsAsync(user))
            {
                return (user, user);
            }
        }

        return ("nobody", "nogroup");
    }

    public async Task FixPermissionsAsync(string path, string user, string group, int fileMode = 644, int dirMode = 755)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            throw new DirectoryNotFoundException($"Path not found: {path}");
        }

        _logger.LogInformation("Fixing permissions for {Path} (user: {User}, group: {Group}, file: {FileMode}, dir: {DirMode})",
            path, user, group, fileMode, dirMode);

        try
        {
            // Set ownership
            await SetOwnershipAsync(path, user, group, recursive: true);

            // Set directory permissions
            await ExecuteCommandAsync("find", $"\"{path}\" -type d -exec chmod {dirMode} {{}} \\;");

            // Set file permissions
            await ExecuteCommandAsync("find", $"\"{path}\" -type f -exec chmod {fileMode} {{}} \\;");

            // Handle special directories that need write access
            var writableDirs = new[]
            {
                "storage",           // Laravel
                "bootstrap/cache",   // Laravel
                "var",               // Symfony
                "cache",             // General
                "logs",              // General
                "tmp",               // General
                "uploads",           // General
                "public/uploads",    // General
                "node_modules/.cache" // Node.js
            };

            foreach (var dir in writableDirs)
            {
                var fullPath = Path.Combine(path, dir);
                if (Directory.Exists(fullPath))
                {
                    await ExecuteCommandAsync("chmod", $"-R 775 \"{fullPath}\"");
                    _logger.LogDebug("Set writable permissions for {Dir}", fullPath);
                }
            }

            _logger.LogInformation("Permissions fixed successfully for {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fix permissions for {Path}", path);
            throw;
        }
    }

    public async Task<List<PermissionIssue>> ValidatePermissionsAsync(string path, string expectedUser, string expectedGroup)
    {
        var issues = new List<PermissionIssue>();

        if (!Directory.Exists(path))
        {
            issues.Add(new PermissionIssue
            {
                Path = path,
                Issue = "Directory does not exist"
            });
            return issues;
        }

        try
        {
            // Check root directory ownership
            var (currentUser, currentGroup) = await GetOwnershipAsync(path);
            if (currentUser != expectedUser || currentGroup != expectedGroup)
            {
                issues.Add(new PermissionIssue
                {
                    Path = path,
                    Issue = "Ownership mismatch",
                    CurrentOwner = $"{currentUser}:{currentGroup}",
                    ExpectedOwner = $"{expectedUser}:{expectedGroup}"
                });
            }

            // Check for common issues
            var criticalPaths = new[]
            {
                ("storage", 775, "Storage directory should be writable"),
                ("bootstrap/cache", 775, "Bootstrap cache should be writable"),
                (".env", 600, "Environment file should be protected"),
                ("config", 755, "Config directory should be readable")
            };

            foreach (var (relativePath, expectedMode, description) in criticalPaths)
            {
                var fullPath = Path.Combine(path, relativePath);
                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                {
                    var result = await ExecuteCommandAsync("stat", $"-c '%a' \"{fullPath}\"");
                    if (int.TryParse(result.Trim().Trim('\''), out var currentMode))
                    {
                        // Check if permissions are too permissive or too restrictive
                        if (relativePath == ".env" && currentMode > 600)
                        {
                            issues.Add(new PermissionIssue
                            {
                                Path = fullPath,
                                Issue = "Permissions too permissive - security risk",
                                CurrentMode = currentMode,
                                ExpectedMode = expectedMode
                            });
                        }
                        else if (relativePath.Contains("storage") || relativePath.Contains("cache"))
                        {
                            if (currentMode < 755)
                            {
                                issues.Add(new PermissionIssue
                                {
                                    Path = fullPath,
                                    Issue = "Permissions too restrictive - may cause write errors",
                                    CurrentMode = currentMode,
                                    ExpectedMode = expectedMode
                                });
                            }
                        }
                    }

                    // Check ownership
                    var (pathUser, pathGroup) = await GetOwnershipAsync(fullPath);
                    if (pathUser != expectedUser)
                    {
                        issues.Add(new PermissionIssue
                        {
                            Path = fullPath,
                            Issue = $"Wrong owner for {description}",
                            CurrentOwner = $"{pathUser}:{pathGroup}",
                            ExpectedOwner = $"{expectedUser}:{expectedGroup}"
                        });
                    }
                }
            }

            // Check for world-writable files (security issue)
            var worldWritableResult = await ExecuteCommandAsync("find", $"\"{path}\" -perm -002 -type f 2>/dev/null | head -10");
            if (!string.IsNullOrWhiteSpace(worldWritableResult))
            {
                var files = worldWritableResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var file in files)
                {
                    issues.Add(new PermissionIssue
                    {
                        Path = file,
                        Issue = "World-writable file - security risk",
                        CurrentMode = 777
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating permissions for {Path}", path);
        }

        return issues;
    }

    /// <summary>
    /// Safely copy files while preserving target directory permissions
    /// </summary>
    public async Task SafeCopyAsync(string source, string destination, string targetUser, string targetGroup)
    {
        _logger.LogDebug("Safe copy: {Source} -> {Destination}", source, destination);

        // Create parent directory if needed
        var parentDir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);
            await SetOwnershipAsync(parentDir, targetUser, targetGroup, recursive: false);
        }

        // Copy file
        File.Copy(source, destination, overwrite: true);

        // Set ownership to target user
        await SetOwnershipAsync(destination, targetUser, targetGroup, recursive: false);

        // Preserve reasonable permissions
        if (destination.EndsWith(".sh") || destination.EndsWith(".py") ||
            Path.GetFileName(destination) == "artisan")
        {
            await ExecuteCommandAsync("chmod", $"755 \"{destination}\"");
        }
        else
        {
            await ExecuteCommandAsync("chmod", $"644 \"{destination}\"");
        }
    }

    /// <summary>
    /// Safely sync a directory while preserving permissions
    /// </summary>
    public async Task SafeSyncDirectoryAsync(string source, string destination, string targetUser, string targetGroup, List<string>? excludePatterns = null)
    {
        _logger.LogInformation("Safe sync: {Source} -> {Destination} as {User}:{Group}",
            source, destination, targetUser, targetGroup);

        var rsyncArgs = new List<string>
        {
            "-av",
            "--delete",
            $"--chown={targetUser}:{targetGroup}",
            "--chmod=D755,F644"
        };

        // Add exclude patterns
        excludePatterns ??= new List<string> { ".git", "node_modules", "vendor", ".env" };
        foreach (var pattern in excludePatterns)
        {
            rsyncArgs.Add($"--exclude='{pattern}'");
        }

        rsyncArgs.Add($"\"{source}/\"");
        rsyncArgs.Add($"\"{destination}/\"");

        await ExecuteCommandAsync("rsync", string.Join(" ", rsyncArgs));

        // Fix special directory permissions
        await FixPermissionsAsync(destination, targetUser, targetGroup);
    }

    private async Task<bool> UserExistsAsync(string username)
    {
        try
        {
            var result = await ExecuteCommandAsync("id", username);
            return !string.IsNullOrEmpty(result);
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> ExecuteCommandAsync(string command, string args)
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

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
            {
                _logger.LogDebug("Command '{Command} {Args}' returned: {Error}", command, args, error);
            }

            return output;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to execute: {Command} {Args}", command, args);
            return "";
        }
    }
}
