using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.Scanner;

/// <summary>
/// AI-powered conflict detection and resolution using Ollama
/// </summary>
public class ConflictResolver : IConflictResolver
{
    private readonly IAiService _aiService;
    private readonly ILogger<ConflictResolver> _logger;

    public ConflictResolver(IAiService aiService, ILogger<ConflictResolver> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<List<SystemConflict>> AnalyzeConflictsAsync(SystemInfo systemInfo, CancellationToken cancellationToken = default)
    {
        var conflicts = new List<SystemConflict>();

        // 1. Check for multiple web servers
        if (systemInfo.DetectedWebServers.Count > 1)
        {
            var runningServers = systemInfo.DetectedWebServers.Where(s => s.IsRunning).ToList();
            if (runningServers.Count > 1)
            {
                conflicts.Add(new SystemConflict
                {
                    Type = ConflictType.MultipleWebServers,
                    Severity = ConflictSeverity.Warning,
                    Title = "Multiple Web Servers Running",
                    Description = $"Found {runningServers.Count} web servers running simultaneously: {string.Join(", ", runningServers.Select(s => s.Name))}",
                    AffectedItems = runningServers.Select(s => s.Name).ToList(),
                    SuggestedResolutions = new List<ConflictResolution>
                    {
                        new()
                        {
                            Id = "stop-extra-servers",
                            Title = "Stop extra web servers",
                            Description = "Keep only the primary web server running",
                            Command = $"systemctl stop {runningServers.Skip(1).First().ServiceName}",
                            IsRecommended = true,
                            Risk = 2
                        },
                        new()
                        {
                            Id = "use-as-reverse-proxy",
                            Title = "Configure as reverse proxy",
                            Description = "Use one server as a reverse proxy for the other",
                            IsRecommended = false,
                            Risk = 5
                        }
                    }
                });
            }
        }

        // 2. Check for port conflicts
        var portUsage = systemInfo.DetectedWebServers
            .SelectMany(s => s.ListeningPorts.Select(p => new { Server = s.Name, Port = p }))
            .GroupBy(x => x.Port)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var portConflict in portUsage)
        {
            conflicts.Add(new SystemConflict
            {
                Type = ConflictType.PortConflict,
                Severity = ConflictSeverity.Error,
                Title = $"Port {portConflict.Key} Conflict",
                Description = $"Multiple services trying to use port {portConflict.Key}: {string.Join(", ", portConflict.Select(x => x.Server))}",
                AffectedItems = portConflict.Select(x => x.Server).ToList(),
                SuggestedResolutions = new List<ConflictResolution>
                {
                    new()
                    {
                        Id = $"change-port-{portConflict.Key}",
                        Title = "Change port configuration",
                        Description = "Modify one service to use a different port",
                        IsRecommended = true,
                        Risk = 3
                    }
                }
            });
        }

        // 3. Check for permission mismatches in sites
        foreach (var site in systemInfo.DiscoveredSites)
        {
            if (!string.IsNullOrEmpty(site.OwnerUser) && systemInfo.WebServer != null)
            {
                var expectedUser = systemInfo.WebServer.RunAsUser;
                if (!string.IsNullOrEmpty(expectedUser) && site.OwnerUser != expectedUser)
                {
                    // Check if web server user can access
                    conflicts.Add(new SystemConflict
                    {
                        Type = ConflictType.PermissionMismatch,
                        Severity = ConflictSeverity.Warning,
                        Title = $"Permission Mismatch: {site.Name}",
                        Description = $"Site owned by {site.OwnerUser}:{site.OwnerGroup} but web server runs as {expectedUser}",
                        AffectedItems = new List<string> { site.DocumentRoot },
                        SuggestedResolutions = new List<ConflictResolution>
                        {
                            new()
                            {
                                Id = "fix-ownership",
                                Title = "Fix ownership",
                                Description = $"Change ownership to {expectedUser}:{expectedUser}",
                                Command = $"chown -R {expectedUser}:{expectedUser} \"{site.DocumentRoot}\"",
                                IsRecommended = false, // Need to analyze first
                                Risk = 4
                            },
                            new()
                            {
                                Id = "add-to-group",
                                Title = "Add user to group",
                                Description = $"Add {expectedUser} to {site.OwnerGroup} group",
                                Command = $"usermod -aG {site.OwnerGroup} {expectedUser}",
                                IsRecommended = true,
                                Risk = 2
                            }
                        }
                    });
                }
            }
        }

        // 4. Check for missing runtimes for detected frameworks
        foreach (var site in systemInfo.DiscoveredSites)
        {
            var requiredRuntime = GetRequiredRuntime(site.DetectedFramework);
            if (requiredRuntime != null)
            {
                var hasRuntime = systemInfo.Runtimes.Any(r => r.Type == requiredRuntime);
                if (!hasRuntime)
                {
                    conflicts.Add(new SystemConflict
                    {
                        Type = ConflictType.MissingDependency,
                        Severity = ConflictSeverity.Error,
                        Title = $"Missing Runtime: {requiredRuntime}",
                        Description = $"Site '{site.Name}' uses {site.DetectedFramework} which requires {requiredRuntime}",
                        AffectedItems = new List<string> { site.Name },
                        SuggestedResolutions = new List<ConflictResolution>
                        {
                            new()
                            {
                                Id = $"install-{requiredRuntime.Value.ToString().ToLower()}",
                                Title = $"Install {requiredRuntime}",
                                Description = GetInstallCommand(requiredRuntime.Value),
                                IsRecommended = true,
                                Risk = 3
                            }
                        }
                    });
                }
            }
        }

        // 5. Check for security risks
        foreach (var site in systemInfo.DiscoveredSites)
        {
            // World-writable directory
            if (site.DirectoryPermissions == 777)
            {
                conflicts.Add(new SystemConflict
                {
                    Type = ConflictType.SecurityRisk,
                    Severity = ConflictSeverity.Critical,
                    Title = $"Security Risk: {site.Name}",
                    Description = "Document root has world-writable permissions (777)",
                    AffectedItems = new List<string> { site.DocumentRoot },
                    SuggestedResolutions = new List<ConflictResolution>
                    {
                        new()
                        {
                            Id = "fix-permissions-755",
                            Title = "Fix permissions to 755",
                            Description = "Set safe directory permissions",
                            Command = $"chmod -R 755 \"{site.DocumentRoot}\"",
                            IsRecommended = true,
                            Risk = 2
                        }
                    }
                });
            }

            // Production with debug enabled
            if (site.Environment == SiteEnvironment.Production &&
                site.EnvironmentIndicators.Any(i => i.Contains("Debug mode: enabled")))
            {
                conflicts.Add(new SystemConflict
                {
                    Type = ConflictType.SecurityRisk,
                    Severity = ConflictSeverity.Warning,
                    Title = $"Debug Mode in Production: {site.Name}",
                    Description = "Debug mode is enabled on a production site",
                    AffectedItems = new List<string> { site.Name },
                    SuggestedResolutions = new List<ConflictResolution>
                    {
                        new()
                        {
                            Id = "disable-debug",
                            Title = "Disable debug mode",
                            Description = "Set APP_DEBUG=false in .env",
                            IsRecommended = true,
                            Risk = 1
                        }
                    }
                });
            }
        }

        // 6. Check resource limits
        if (systemInfo.AvailableDiskGb < 5)
        {
            conflicts.Add(new SystemConflict
            {
                Type = ConflictType.ResourceLimit,
                Severity = ConflictSeverity.Warning,
                Title = "Low Disk Space",
                Description = $"Only {systemInfo.AvailableDiskGb}GB of disk space available",
                SuggestedResolutions = new List<ConflictResolution>
                {
                    new()
                    {
                        Id = "clean-logs",
                        Title = "Clean old logs",
                        Description = "Remove old log files to free space",
                        Command = "find /var/log -type f -name '*.log' -mtime +30 -delete",
                        IsRecommended = true,
                        Risk = 2
                    }
                }
            });
        }

        // Get AI recommendations for each conflict
        foreach (var conflict in conflicts)
        {
            try
            {
                conflict.AiRecommendation = await GetAiRecommendationAsync(conflict, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to get AI recommendation for conflict {Id}", conflict.Id);
            }
        }

        return conflicts;
    }

    public async Task<string> GetAiRecommendationAsync(SystemConflict conflict, CancellationToken cancellationToken = default)
    {
        var prompt = $"""
            You are a Linux system administrator helping to resolve a system conflict.

            Conflict Type: {conflict.Type}
            Severity: {conflict.Severity}
            Title: {conflict.Title}
            Description: {conflict.Description}
            Affected Items: {string.Join(", ", conflict.AffectedItems)}

            Available Resolutions:
            {string.Join("\n", conflict.SuggestedResolutions.Select(r => $"- {r.Title}: {r.Description}"))}

            Please provide a brief recommendation (2-3 sentences) on:
            1. Which resolution is best and why
            2. Any precautions to take
            3. Potential side effects to watch for

            Keep your response concise and technical.
            """;

        try
        {
            var context = new ErrorContext
            {
                ProjectName = conflict.Title,
                ErrorMessage = prompt,
                Framework = FrameworkType.Unknown
            };
            var response = await _aiService.AnalyzeErrorAsync(context, cancellationToken);
            return response.Summary ?? response.Explanation ?? "AI analysis not available. Please review manually.";
        }
        catch
        {
            return "AI analysis not available. Please review the suggested resolutions manually.";
        }
    }

    public async Task<bool> ApplyResolutionAsync(SystemConflict conflict, string resolutionId, CancellationToken cancellationToken = default)
    {
        var resolution = conflict.SuggestedResolutions.FirstOrDefault(r => r.Id == resolutionId);
        if (resolution == null)
        {
            _logger.LogWarning("Resolution {ResolutionId} not found for conflict {ConflictId}", resolutionId, conflict.Id);
            return false;
        }

        if (string.IsNullOrEmpty(resolution.Command))
        {
            _logger.LogWarning("Resolution {ResolutionId} has no command", resolutionId);
            return false;
        }

        _logger.LogInformation("Applying resolution {ResolutionId} for conflict {ConflictId}: {Command}",
            resolutionId, conflict.Id, resolution.Command);

        try
        {
            var result = await ExecuteCommandAsync(resolution.Command);
            conflict.IsResolved = true;
            conflict.ResolutionApplied = resolutionId;

            _logger.LogInformation("Resolution applied successfully");

            if (resolution.RequiresRestart)
            {
                _logger.LogInformation("Service restart may be required");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply resolution {ResolutionId}", resolutionId);
            return false;
        }
    }

    public async Task<int> AutoResolveAsync(List<SystemConflict> conflicts, CancellationToken cancellationToken = default)
    {
        var resolved = 0;

        // Only auto-resolve low-risk conflicts
        var safeConflicts = conflicts
            .Where(c => !c.IsResolved)
            .Where(c => c.Severity != ConflictSeverity.Critical)
            .Where(c => c.SuggestedResolutions.Any(r => r.IsRecommended && r.Risk <= 2))
            .ToList();

        foreach (var conflict in safeConflicts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeResolution = conflict.SuggestedResolutions
                .Where(r => r.IsRecommended && r.Risk <= 2)
                .FirstOrDefault();

            if (safeResolution != null)
            {
                var success = await ApplyResolutionAsync(conflict, safeResolution.Id, cancellationToken);
                if (success)
                {
                    resolved++;
                }
            }
        }

        _logger.LogInformation("Auto-resolved {Count} conflicts", resolved);
        return resolved;
    }

    private RuntimeType? GetRequiredRuntime(Core.Enums.FrameworkType framework)
    {
        return framework switch
        {
            Core.Enums.FrameworkType.Laravel => RuntimeType.Php,
            Core.Enums.FrameworkType.NodeJs or
            Core.Enums.FrameworkType.React or
            Core.Enums.FrameworkType.Vue => RuntimeType.NodeJs,
            Core.Enums.FrameworkType.Django => RuntimeType.Python,
            Core.Enums.FrameworkType.DotNet => RuntimeType.DotNet,
            _ => null
        };
    }

    private string GetInstallCommand(RuntimeType runtime)
    {
        return runtime switch
        {
            RuntimeType.Php => "apt install php php-fpm php-mysql php-xml php-curl php-mbstring",
            RuntimeType.NodeJs => "curl -fsSL https://deb.nodesource.com/setup_lts.x | bash - && apt install nodejs",
            RuntimeType.Python => "apt install python3 python3-pip python3-venv",
            RuntimeType.DotNet => "apt install dotnet-sdk-8.0",
            RuntimeType.Ruby => "apt install ruby ruby-dev",
            RuntimeType.Go => "apt install golang-go",
            RuntimeType.Java => "apt install openjdk-17-jdk",
            _ => "# Please install manually"
        };
    }

    private async Task<string> ExecuteCommandAsync(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new Exception("Failed to start process");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Command failed with exit code {process.ExitCode}: {error}");
        }

        return output;
    }
}
