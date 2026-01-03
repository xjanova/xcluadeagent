namespace XcluadeAgent.Shared.DTOs;

/// <summary>
/// Complete settings DTO
/// </summary>
public class SettingsDto
{
    public ServerSettingsDto Server { get; set; } = new();
    public SyncSettingsDto Sync { get; set; } = new();
    public GitHubSettingsDto GitHub { get; set; } = new();
    public AiSettingsDto Ai { get; set; } = new();
    public NotificationSettingsDto Notifications { get; set; } = new();
    public SecuritySettingsDto Security { get; set; } = new();
}

/// <summary>
/// Server settings
/// </summary>
public class ServerSettingsDto
{
    public int Port { get; set; } = 5000;
    public string? ExternalUrl { get; set; }
    public string DataDirectory { get; set; } = "data";
}

/// <summary>
/// Sync settings
/// </summary>
public class SyncSettingsDto
{
    public bool AutoSync { get; set; } = true;
    public bool BackupBeforeSync { get; set; } = true;
    public int BackupRetentionDays { get; set; } = 30;
}

/// <summary>
/// GitHub settings
/// </summary>
public class GitHubSettingsDto
{
    public bool HasToken { get; set; }
    public string? WebhookSecret { get; set; }
    public string? WebhookUrl { get; set; }
}

/// <summary>
/// AI settings
/// </summary>
public class AiSettingsDto
{
    public bool Enabled { get; set; }
    public string Mode { get; set; } = "Off";
    public string Provider { get; set; } = "Ollama";
    public string? OllamaEndpoint { get; set; }
    public string? OllamaModel { get; set; }
    public bool HasClaudeKey { get; set; }
    public string? ClaudeModel { get; set; }
    public bool HasOpenAiKey { get; set; }
    public string? OpenAiModel { get; set; }
    public bool BudgetEnabled { get; set; }
    public decimal MonthlyBudgetUsd { get; set; }
    public decimal CurrentUsageUsd { get; set; }
}

/// <summary>
/// Notification settings
/// </summary>
public class NotificationSettingsDto
{
    public bool EmailEnabled { get; set; }
    public string? EmailSmtpHost { get; set; }
    public int EmailSmtpPort { get; set; }
    public string? EmailFromAddress { get; set; }

    public bool DiscordEnabled { get; set; }
    public bool HasDiscordWebhook { get; set; }

    public bool TelegramEnabled { get; set; }
    public bool HasTelegramToken { get; set; }

    public bool SlackEnabled { get; set; }
    public bool HasSlackWebhook { get; set; }

    public bool LineEnabled { get; set; }
    public bool HasLineToken { get; set; }
}

/// <summary>
/// Security settings
/// </summary>
public class SecuritySettingsDto
{
    public int SessionTimeoutMinutes { get; set; } = 60;
    public bool Require2FaForAdmins { get; set; }
    public bool ApiEnabled { get; set; } = true;
    public int RateLimitPerMinute { get; set; } = 100;
}
