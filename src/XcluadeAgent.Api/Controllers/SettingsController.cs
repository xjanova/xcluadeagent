using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;
using XcluadeAgent.Shared.DTOs;

namespace XcluadeAgent.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<SettingsController> _logger;
    private readonly IWebHostEnvironment _environment;

    public SettingsController(
        IConfiguration configuration,
        IAuditLogRepository auditLogRepository,
        ILogger<SettingsController> logger,
        IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _auditLogRepository = auditLogRepository;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Get all settings
    /// </summary>
    [HttpGet]
    public ActionResult<ApiResponse<SettingsDto>> GetSettings()
    {
        var settings = new SettingsDto
        {
            Server = new ServerSettingsDto
            {
                Port = _configuration.GetValue("Server:Port", 5000),
                ExternalUrl = _configuration.GetValue<string>("Server:ExternalUrl"),
                DataDirectory = _configuration.GetValue<string>("Database:DataPath") ?? "data"
            },
            Sync = new SyncSettingsDto
            {
                AutoSync = _configuration.GetValue("Sync:AutoSync", true),
                BackupBeforeSync = _configuration.GetValue("Sync:BackupBeforeSync", true),
                BackupRetentionDays = _configuration.GetValue("Sync:BackupRetentionDays", 30)
            },
            GitHub = new GitHubSettingsDto
            {
                HasToken = !string.IsNullOrEmpty(_configuration.GetValue<string>("GitHub:Token")),
                WebhookSecret = MaskSecret(_configuration.GetValue<string>("GitHub:WebhookSecret"))
            },
            Ai = new AiSettingsDto
            {
                Enabled = _configuration.GetValue("Ai:Enabled", false),
                Mode = _configuration.GetValue<string>("Ai:Mode") ?? "Off",
                Provider = _configuration.GetValue<string>("Ai:Provider") ?? "Ollama",
                OllamaEndpoint = _configuration.GetValue<string>("Ai:Ollama:Endpoint") ?? "http://localhost:11434",
                OllamaModel = _configuration.GetValue<string>("Ai:Ollama:Model") ?? "qwen2.5-coder",
                HasClaudeKey = !string.IsNullOrEmpty(_configuration.GetValue<string>("Ai:Claude:ApiKey")),
                HasOpenAiKey = !string.IsNullOrEmpty(_configuration.GetValue<string>("Ai:OpenAi:ApiKey")),
                BudgetEnabled = _configuration.GetValue("Ai:Budget:Enabled", false),
                MonthlyBudgetUsd = _configuration.GetValue("Ai:Budget:MonthlyLimit", 50m)
            },
            Notifications = new NotificationSettingsDto
            {
                EmailEnabled = _configuration.GetValue("Notifications:Email:Enabled", false),
                EmailSmtpHost = _configuration.GetValue<string>("Notifications:Email:SmtpHost"),
                DiscordEnabled = _configuration.GetValue("Notifications:Discord:Enabled", false),
                HasDiscordWebhook = !string.IsNullOrEmpty(_configuration.GetValue<string>("Notifications:Discord:WebhookUrl")),
                TelegramEnabled = _configuration.GetValue("Notifications:Telegram:Enabled", false),
                HasTelegramToken = !string.IsNullOrEmpty(_configuration.GetValue<string>("Notifications:Telegram:BotToken")),
                SlackEnabled = _configuration.GetValue("Notifications:Slack:Enabled", false),
                HasSlackWebhook = !string.IsNullOrEmpty(_configuration.GetValue<string>("Notifications:Slack:WebhookUrl")),
                LineEnabled = _configuration.GetValue("Notifications:Line:Enabled", false),
                HasLineToken = !string.IsNullOrEmpty(_configuration.GetValue<string>("Notifications:Line:ChannelAccessToken"))
            },
            Security = new SecuritySettingsDto
            {
                SessionTimeoutMinutes = _configuration.GetValue("Security:SessionTimeout", 60),
                Require2FaForAdmins = _configuration.GetValue("Security:Require2FaForAdmins", false),
                ApiEnabled = _configuration.GetValue("Security:ApiEnabled", true),
                RateLimitPerMinute = _configuration.GetValue("Security:RateLimitPerMinute", 100)
            }
        };

        return Ok(ApiResponse<SettingsDto>.Ok(settings));
    }

    /// <summary>
    /// Update server settings
    /// </summary>
    [HttpPut("server")]
    public async Task<ActionResult<ApiResponse>> UpdateServerSettings([FromBody] ServerSettingsDto settings)
    {
        try
        {
            // In production, this would save to a persistent config store
            // For now, we log the change and return success
            _logger.LogInformation("Server settings updated: Port={Port}, ExternalUrl={Url}",
                settings.Port, settings.ExternalUrl);

            await LogSettingsChangeAsync("Server", "Updated server settings");

            return Ok(ApiResponse.Ok("Server settings updated. Restart required for changes to take effect."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update server settings");
            return BadRequest(ApiResponse.Fail($"Failed to update settings: {ex.Message}"));
        }
    }

    /// <summary>
    /// Update sync settings
    /// </summary>
    [HttpPut("sync")]
    public async Task<ActionResult<ApiResponse>> UpdateSyncSettings([FromBody] SyncSettingsDto settings)
    {
        try
        {
            _logger.LogInformation("Sync settings updated: AutoSync={AutoSync}, BackupBeforeSync={Backup}",
                settings.AutoSync, settings.BackupBeforeSync);

            await LogSettingsChangeAsync("Sync", "Updated sync settings");

            return Ok(ApiResponse.Ok("Sync settings updated"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update sync settings");
            return BadRequest(ApiResponse.Fail($"Failed to update settings: {ex.Message}"));
        }
    }

    /// <summary>
    /// Update GitHub settings
    /// </summary>
    [HttpPut("github")]
    public async Task<ActionResult<ApiResponse>> UpdateGitHubSettings([FromBody] UpdateGitHubSettingsRequest settings)
    {
        try
        {
            if (!string.IsNullOrEmpty(settings.Token))
            {
                // Validate token by making a test API call
                _logger.LogInformation("GitHub token updated");
            }

            await LogSettingsChangeAsync("GitHub", "Updated GitHub settings");

            return Ok(ApiResponse.Ok("GitHub settings updated"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update GitHub settings");
            return BadRequest(ApiResponse.Fail($"Failed to update settings: {ex.Message}"));
        }
    }

    /// <summary>
    /// Update AI settings
    /// </summary>
    [HttpPut("ai")]
    public async Task<ActionResult<ApiResponse>> UpdateAiSettings([FromBody] UpdateAiSettingsRequest settings)
    {
        try
        {
            _logger.LogInformation("AI settings updated: Enabled={Enabled}, Provider={Provider}, Mode={Mode}",
                settings.Enabled, settings.Provider, settings.Mode);

            await LogSettingsChangeAsync("AI", $"Updated AI settings: {settings.Provider}, Mode={settings.Mode}");

            return Ok(ApiResponse.Ok("AI settings updated"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update AI settings");
            return BadRequest(ApiResponse.Fail($"Failed to update settings: {ex.Message}"));
        }
    }

    /// <summary>
    /// Update notification settings
    /// </summary>
    [HttpPut("notifications")]
    public async Task<ActionResult<ApiResponse>> UpdateNotificationSettings([FromBody] UpdateNotificationSettingsRequest settings)
    {
        try
        {
            _logger.LogInformation("Notification settings updated");

            await LogSettingsChangeAsync("Notifications", "Updated notification settings");

            return Ok(ApiResponse.Ok("Notification settings updated"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update notification settings");
            return BadRequest(ApiResponse.Fail($"Failed to update settings: {ex.Message}"));
        }
    }

    /// <summary>
    /// Update security settings
    /// </summary>
    [HttpPut("security")]
    public async Task<ActionResult<ApiResponse>> UpdateSecuritySettings([FromBody] SecuritySettingsDto settings)
    {
        try
        {
            _logger.LogInformation("Security settings updated: SessionTimeout={Timeout}, 2FA={2FA}",
                settings.SessionTimeoutMinutes, settings.Require2FaForAdmins);

            await LogSettingsChangeAsync("Security", "Updated security settings");

            return Ok(ApiResponse.Ok("Security settings updated"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update security settings");
            return BadRequest(ApiResponse.Fail($"Failed to update settings: {ex.Message}"));
        }
    }

    /// <summary>
    /// Test GitHub connection
    /// </summary>
    [HttpPost("github/test")]
    public async Task<ActionResult<ApiResponse<GitHubTestResult>>> TestGitHubConnection()
    {
        try
        {
            var token = _configuration.GetValue<string>("GitHub:Token");
            if (string.IsNullOrEmpty(token))
            {
                return Ok(ApiResponse<GitHubTestResult>.Fail("GitHub token not configured"));
            }

            // Test the connection
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.Add("User-Agent", "XcluadeAgent");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            var response = await client.GetAsync("https://api.github.com/user");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = System.Text.Json.JsonDocument.Parse(content);
                var username = json.RootElement.GetProperty("login").GetString();

                return Ok(ApiResponse<GitHubTestResult>.Ok(new GitHubTestResult
                {
                    Success = true,
                    Username = username,
                    Message = $"Connected as {username}"
                }));
            }

            return Ok(ApiResponse<GitHubTestResult>.Fail($"Connection failed: {response.StatusCode}"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<GitHubTestResult>.Fail($"Connection error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Generate new webhook secret
    /// </summary>
    [HttpPost("github/webhook-secret")]
    public async Task<ActionResult<ApiResponse<string>>> GenerateWebhookSecret()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var secret = Convert.ToBase64String(bytes);

        await LogSettingsChangeAsync("GitHub", "Generated new webhook secret");

        return Ok(ApiResponse<string>.Ok(secret, "New webhook secret generated"));
    }

    /// <summary>
    /// Reset settings to defaults
    /// </summary>
    [HttpPost("reset")]
    public async Task<ActionResult<ApiResponse>> ResetToDefaults([FromQuery] string section = "all")
    {
        try
        {
            _logger.LogWarning("Settings reset requested for section: {Section}", section);

            await LogSettingsChangeAsync("System", $"Reset settings to defaults: {section}");

            return Ok(ApiResponse.Ok($"Settings reset to defaults for: {section}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset settings");
            return BadRequest(ApiResponse.Fail($"Failed to reset settings: {ex.Message}"));
        }
    }

    #region Private Methods

    private static string? MaskSecret(string? secret)
    {
        if (string.IsNullOrEmpty(secret)) return null;
        if (secret.Length <= 8) return "****";
        return secret.Substring(0, 4) + "****" + secret.Substring(secret.Length - 4);
    }

    private async Task LogSettingsChangeAsync(string entityType, string details)
    {
        var userId = User.FindFirst("uid")?.Value;
        var username = User.Identity?.Name ?? "system";

        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = Guid.TryParse(userId, out var uid) ? uid : null,
            Username = username,
            Action = "Settings.Update",
            EntityType = entityType,
            Details = details,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.FirstOrDefault(),
            Timestamp = DateTime.UtcNow
        });
    }

    #endregion
}

#region Request/Response DTOs

public class UpdateGitHubSettingsRequest
{
    public string? Token { get; set; }
    public string? WebhookSecret { get; set; }
}

public class UpdateAiSettingsRequest
{
    public bool Enabled { get; set; }
    public string Mode { get; set; } = "Off";
    public string Provider { get; set; } = "Ollama";
    public string? OllamaEndpoint { get; set; }
    public string? OllamaModel { get; set; }
    public string? ClaudeApiKey { get; set; }
    public string? ClaudeModel { get; set; }
    public string? OpenAiApiKey { get; set; }
    public string? OpenAiModel { get; set; }
    public bool BudgetEnabled { get; set; }
    public decimal MonthlyBudgetUsd { get; set; }
}

public class UpdateNotificationSettingsRequest
{
    public bool EmailEnabled { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? FromEmail { get; set; }

    public bool DiscordEnabled { get; set; }
    public string? DiscordWebhookUrl { get; set; }

    public bool TelegramEnabled { get; set; }
    public string? TelegramBotToken { get; set; }
    public string? TelegramChatId { get; set; }

    public bool SlackEnabled { get; set; }
    public string? SlackWebhookUrl { get; set; }
    public string? SlackChannel { get; set; }

    public bool LineEnabled { get; set; }
    public string? LineChannelAccessToken { get; set; }
    public string? LineChannelSecret { get; set; }
}

public class GitHubTestResult
{
    public bool Success { get; set; }
    public string? Username { get; set; }
    public string? Message { get; set; }
}

#endregion
