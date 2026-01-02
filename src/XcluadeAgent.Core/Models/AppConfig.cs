using XcluadeAgent.Core.Enums;

namespace XcluadeAgent.Core.Models;

/// <summary>
/// Application configuration (loaded from YAML)
/// </summary>
public class AppConfig
{
    /// <summary>
    /// Application info
    /// </summary>
    public AppInfo App { get; set; } = new();

    /// <summary>
    /// Server settings
    /// </summary>
    public ServerConfig Server { get; set; } = new();

    /// <summary>
    /// Database settings
    /// </summary>
    public DatabaseConfig Database { get; set; } = new();

    /// <summary>
    /// GitHub settings
    /// </summary>
    public GitHubConfig GitHub { get; set; } = new();

    /// <summary>
    /// AI settings
    /// </summary>
    public AiConfig Ai { get; set; } = new();

    /// <summary>
    /// Notification settings
    /// </summary>
    public NotificationConfig Notifications { get; set; } = new();

    /// <summary>
    /// Security settings
    /// </summary>
    public SecurityConfig Security { get; set; } = new();

    /// <summary>
    /// Backup settings
    /// </summary>
    public BackupConfig Backup { get; set; } = new();

    /// <summary>
    /// License settings
    /// </summary>
    public LicenseConfig License { get; set; } = new();

    /// <summary>
    /// Logging settings
    /// </summary>
    public LoggingConfig Logging { get; set; } = new();
}

/// <summary>
/// Application info
/// </summary>
public class AppInfo
{
    public string Name { get; set; } = "XcluadeAgent";
    public string Version { get; set; } = "1.0.0";
    public string Developer { get; set; } = "xman studio";
    public string Website { get; set; } = "https://xman4289.com";
}

/// <summary>
/// Server configuration
/// </summary>
public class ServerConfig
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5000;
    public string? BasePath { get; set; }
    public bool UseHttps { get; set; } = false;
    public string? CertificatePath { get; set; }
    public string? CertificatePassword { get; set; }
    public string? ExternalUrl { get; set; }
}

/// <summary>
/// Database configuration
/// </summary>
public class DatabaseConfig
{
    public string Provider { get; set; } = "sqlite";
    public string ConnectionString { get; set; } = "Data Source=data/xcluadeagent.db";
}

/// <summary>
/// GitHub configuration
/// </summary>
public class GitHubConfig
{
    /// <summary>
    /// Personal access token
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Webhook secret for verification
    /// </summary>
    public string? WebhookSecret { get; set; }

    /// <summary>
    /// API base URL (for GitHub Enterprise)
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://api.github.com";

    /// <summary>
    /// Rate limit handling
    /// </summary>
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
}

/// <summary>
/// AI configuration
/// </summary>
public class AiConfig
{
    /// <summary>
    /// Is AI enabled globally
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Default AI mode
    /// </summary>
    public AiMode Mode { get; set; } = AiMode.Suggest;

    /// <summary>
    /// Primary AI provider
    /// </summary>
    public AiProviderConfig Primary { get; set; } = new()
    {
        Type = AiProvider.Ollama,
        Model = "qwen2.5-coder:7b",
        Endpoint = "http://localhost:11434"
    };

    /// <summary>
    /// Fallback AI provider (for complex tasks)
    /// </summary>
    public AiProviderConfig? Fallback { get; set; }

    /// <summary>
    /// Cost control settings
    /// </summary>
    public AiCostControl CostControl { get; set; } = new();

    /// <summary>
    /// Safety settings
    /// </summary>
    public AiSafetyConfig Safety { get; set; } = new();
}

/// <summary>
/// AI provider configuration
/// </summary>
public class AiProviderConfig
{
    public AiProvider Type { get; set; } = AiProvider.None;
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public string? Model { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxTokens { get; set; } = 4096;
}

/// <summary>
/// AI cost control
/// </summary>
public class AiCostControl
{
    public decimal MonthlyBudgetUsd { get; set; } = 10;
    public int WarnAtPercent { get; set; } = 80;
    public bool StopAtLimit { get; set; } = true;
}

/// <summary>
/// AI safety configuration
/// </summary>
public class AiSafetyConfig
{
    public bool RequireApproval { get; set; } = true;
    public int MaxFilesPerFix { get; set; } = 5;
    public List<string> ExcludedPaths { get; set; } = [".env*", "**/config/*", "**/credentials/*", "**/*.key", "**/*.pem"];
    public bool AllowDatabaseMigrations { get; set; } = false;
    public bool RequireTestPass { get; set; } = true;
}

/// <summary>
/// Notification configuration
/// </summary>
public class NotificationConfig
{
    public EmailConfig? Email { get; set; }
    public DiscordConfig? Discord { get; set; }
    public TelegramConfig? Telegram { get; set; }
    public LineOAConfig? LineOA { get; set; }
    public SlackConfig? Slack { get; set; }
    public List<WebhookConfig> Webhooks { get; set; } = [];
}

public class EmailConfig
{
    public bool Enabled { get; set; } = false;
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FromAddress { get; set; }
    public string? FromName { get; set; } = "XcluadeAgent";
}

public class DiscordConfig
{
    public bool Enabled { get; set; } = false;
    public string? WebhookUrl { get; set; }
    public string? BotToken { get; set; }
    public string? ChannelId { get; set; }
}

public class TelegramConfig
{
    public bool Enabled { get; set; } = false;
    public string? BotToken { get; set; }
    public string? DefaultChatId { get; set; }
}

public class LineOAConfig
{
    public bool Enabled { get; set; } = false;
    public string? ChannelAccessToken { get; set; }
    public string? ChannelSecret { get; set; }
    public string? DefaultUserId { get; set; }
}

public class SlackConfig
{
    public bool Enabled { get; set; } = false;
    public string? WebhookUrl { get; set; }
    public string? BotToken { get; set; }
    public string? DefaultChannel { get; set; }
}

public class WebhookConfig
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Secret { get; set; }
    public List<string> Events { get; set; } = ["sync.completed", "sync.failed"];
}

/// <summary>
/// Security configuration
/// </summary>
public class SecurityConfig
{
    public string JwtSecret { get; set; } = string.Empty;
    public int JwtExpirationMinutes { get; set; } = 1440; // 24 hours
    public int RefreshTokenExpirationDays { get; set; } = 30;
    public int MaxFailedLogins { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public bool RequireTwoFactor { get; set; } = false;
    public List<string> AllowedOrigins { get; set; } = [];
    public List<string> TrustedProxies { get; set; } = [];
}

/// <summary>
/// Backup configuration
/// </summary>
public class BackupConfig
{
    public string BasePath { get; set; } = "data/backups";
    public int DefaultMaxBackups { get; set; } = 5;
    public string CompressionType { get; set; } = "gzip"; // none, gzip, zip
    public bool IncludeDatabase { get; set; } = true;
}

/// <summary>
/// License configuration
/// </summary>
public class LicenseConfig
{
    public string? Key { get; set; }
    public string ValidationEndpoint { get; set; } = "https://api.github.com/repos/xjanova/xmanstudio/contents/licenses";
    public int ValidationIntervalHours { get; set; } = 24;
    public bool AllowOffline { get; set; } = true;
    public int OfflineGraceDays { get; set; } = 7;
}

/// <summary>
/// Logging configuration
/// </summary>
public class LoggingConfig
{
    public string Level { get; set; } = "Information";
    public string Path { get; set; } = "data/logs";
    public int RetentionDays { get; set; } = 30;
    public bool EnableConsole { get; set; } = true;
    public bool EnableFile { get; set; } = true;
}
