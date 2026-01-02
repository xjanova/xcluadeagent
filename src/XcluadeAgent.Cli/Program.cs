using System.CommandLine;
using System.Net.Http.Json;
using Spectre.Console;
using XcluadeAgent.Shared.DTOs;

// XcluadeAgent CLI - syncctl
// Developed by xman studio | https://xman4289.com

var rootCommand = new RootCommand("XcluadeAgent CLI - GitHub Sync Service Controller");

// Configuration
var apiUrlOption = new Option<string>(
    name: "--api",
    description: "API URL");
apiUrlOption.AddAlias("-a");
apiUrlOption.SetDefaultValue("http://localhost:5000");

var tokenOption = new Option<string?>(
    name: "--token",
    description: "JWT authentication token");
tokenOption.AddAlias("-t");

rootCommand.AddGlobalOption(apiUrlOption);
rootCommand.AddGlobalOption(tokenOption);

// Status command
var statusCommand = new Command("status", "Show system status");
statusCommand.SetHandler(async (string apiUrl, string? token) =>
{
    using var client = CreateClient(apiUrl, token);

    AnsiConsole.Write(new FigletText("XcluadeAgent").Color(Color.Blue));
    AnsiConsole.MarkupLine("[grey]by xman studio | xman4289.com[/]");
    AnsiConsole.WriteLine();

    await AnsiConsole.Status()
        .StartAsync("Fetching status...", async ctx =>
        {
            try
            {
                var health = await client.GetFromJsonAsync<dynamic>("health");
                var table = new Table();
                table.AddColumn("Property");
                table.AddColumn("Value");

                table.AddRow("Status", "[green]Healthy[/]");
                table.AddRow("API URL", apiUrl);
                table.AddRow("Version", "1.0.0");

                AnsiConsole.Write(table);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            }
        });
}, apiUrlOption, tokenOption);
rootCommand.AddCommand(statusCommand);

// List projects command
var listCommand = new Command("list", "List all projects");
listCommand.SetHandler(async (string apiUrl, string? token) =>
{
    using var client = CreateClient(apiUrl, token);

    await AnsiConsole.Status()
        .StartAsync("Fetching projects...", async ctx =>
        {
            try
            {
                var response = await client.GetFromJsonAsync<ApiResponse<List<ProjectDto>>>("api/v1/projects");

                if (response?.Data == null || response.Data.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No projects found.[/]");
                    return;
                }

                var table = new Table();
                table.AddColumn("Name");
                table.AddColumn("Status");
                table.AddColumn("Framework");
                table.AddColumn("Last Sync");

                foreach (var project in response.Data)
                {
                    var statusColor = project.Status switch
                    {
                        "Synced" => "green",
                        "Pending" => "yellow",
                        "Failed" => "red",
                        _ => "grey"
                    };

                    table.AddRow(
                        project.Name,
                        $"[{statusColor}]{project.Status}[/]",
                        project.Framework,
                        project.LastSyncAt?.ToString("g") ?? "Never"
                    );
                }

                AnsiConsole.Write(table);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            }
        });
}, apiUrlOption, tokenOption);
rootCommand.AddCommand(listCommand);

// Sync command
var syncCommand = new Command("sync", "Sync a project");
var projectArg = new Argument<string>("project", "Project name or ID");
var dryRunOption = new Option<bool>("--dry-run", "Preview changes without applying");
syncCommand.AddArgument(projectArg);
syncCommand.AddOption(dryRunOption);

syncCommand.SetHandler(async (string project, bool dryRun, string apiUrl, string? token) =>
{
    using var client = CreateClient(apiUrl, token);

    AnsiConsole.MarkupLine($"[blue]Syncing project: {project}[/]");

    if (dryRun)
    {
        AnsiConsole.MarkupLine("[yellow]Dry run mode - no changes will be applied[/]");
    }

    await AnsiConsole.Progress()
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask("Syncing...");

            try
            {
                var request = new { dryRun };
                var response = await client.PostAsJsonAsync($"api/v1/projects/{project}/sync", request);

                if (response.IsSuccessStatusCode)
                {
                    task.Value = 100;
                    AnsiConsole.MarkupLine("[green]Sync completed successfully![/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Sync failed: {response.StatusCode}[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            }
        });
}, projectArg, dryRunOption, apiUrlOption, tokenOption);
rootCommand.AddCommand(syncCommand);

// Rollback command
var rollbackCommand = new Command("rollback", "Rollback a project");
rollbackCommand.AddArgument(projectArg);
var versionOption = new Option<string?>("--version", "Version to rollback to");
rollbackCommand.AddOption(versionOption);

rollbackCommand.SetHandler(async (string project, string? version, string apiUrl, string? token) =>
{
    using var client = CreateClient(apiUrl, token);

    if (!AnsiConsole.Confirm($"Rollback project [yellow]{project}[/]?"))
    {
        return;
    }

    AnsiConsole.MarkupLine($"[blue]Rolling back project: {project}[/]");

    try
    {
        var request = new { toLastBackup = version == null };
        var response = await client.PostAsJsonAsync($"api/v1/projects/{project}/rollback", request);

        if (response.IsSuccessStatusCode)
        {
            AnsiConsole.MarkupLine("[green]Rollback completed successfully![/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Rollback failed: {response.StatusCode}[/]");
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
    }
}, projectArg, versionOption, apiUrlOption, tokenOption);
rootCommand.AddCommand(rollbackCommand);

// Login command
var loginCommand = new Command("login", "Login to get authentication token");
loginCommand.SetHandler(async (string apiUrl) =>
{
    var username = AnsiConsole.Ask<string>("Username:");
    var password = AnsiConsole.Prompt(
        new TextPrompt<string>("Password:")
            .Secret());

    using var client = new HttpClient { BaseAddress = new Uri(apiUrl) };

    try
    {
        var response = await client.PostAsJsonAsync("api/v1/auth/login", new { username, password });
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

        if (result?.Success == true)
        {
            AnsiConsole.MarkupLine("[green]Login successful![/]");
            AnsiConsole.MarkupLine($"Token: [grey]{result.Token}[/]");
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("Use with: syncctl --token <token> <command>");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Login failed: {result?.ErrorMessage}[/]");
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
    }
}, apiUrlOption);
rootCommand.AddCommand(loginCommand);

// Version command
var versionCommand = new Command("version", "Show version information");
versionCommand.SetHandler(() =>
{
    AnsiConsole.Write(new FigletText("syncctl").Color(Color.Blue));
    AnsiConsole.MarkupLine("XcluadeAgent CLI v1.0.0");
    AnsiConsole.MarkupLine("[grey]Developed by xman studio | https://xman4289.com[/]");
});
rootCommand.AddCommand(versionCommand);

return await rootCommand.InvokeAsync(args);

// Helper
static HttpClient CreateClient(string apiUrl, string? token)
{
    var client = new HttpClient { BaseAddress = new Uri(apiUrl) };
    if (!string.IsNullOrEmpty(token))
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
    return client;
}
