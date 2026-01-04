using XcluadeAgent.Core.Enums;

namespace XcluadeAgent.Shared.DTOs;

/// <summary>
/// Dashboard overview data
/// </summary>
public class DashboardOverviewDto
{
    public int TotalProjects { get; set; }
    public int SyncedProjects { get; set; }
    public int PendingProjects { get; set; }
    public int FailedProjects { get; set; }
    public string TotalBackupSize { get; set; } = string.Empty;
    public int BackupCount { get; set; }
    public int SyncsToday { get; set; }
    public int SyncsThisWeek { get; set; }
    public SystemHealthDto SystemHealth { get; set; } = new();
    public LicenseInfoDto License { get; set; } = new();
    public AiStatusDto AiStatus { get; set; } = new();
    public List<ProjectSummaryDto> RecentProjects { get; set; } = [];
    public List<ActivityDto> RecentActivity { get; set; } = [];
}

/// <summary>
/// System health status
/// </summary>
public class SystemHealthDto
{
    public string Status { get; set; } = "healthy";
    public string StatusColor { get; set; } = "green";
    public string DiskUsage { get; set; } = string.Empty;
    public int DiskUsagePercent { get; set; }
    public string MemoryUsage { get; set; } = string.Empty;
    public int MemoryUsagePercent { get; set; }
    public string Uptime { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime LastCheck { get; set; }
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// License info for dashboard
/// </summary>
public class LicenseInfoDto
{
    public string Type { get; set; } = string.Empty;
    public string TypeColor { get; set; } = string.Empty;
    public string LicensedTo { get; set; } = string.Empty;
    public string? ExpiresAt { get; set; }
    public int? DaysRemaining { get; set; }
    public bool IsExpiringSoon { get; set; }
    public int ProjectsUsed { get; set; }
    public int ProjectsLimit { get; set; }
    public int UsersUsed { get; set; }
    public int UsersLimit { get; set; }
    public List<string> Features { get; set; } = [];
}

/// <summary>
/// AI status for dashboard
/// </summary>
public class AiStatusDto
{
    public bool Enabled { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string ModeDescription { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string RiskColor { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? Model { get; set; }
    public bool HasGpu { get; set; }
    public string BudgetUsed { get; set; } = string.Empty;
    public string BudgetRemaining { get; set; } = string.Empty;
    public int BudgetUsedPercent { get; set; }
    public int RequestsToday { get; set; }
}

/// <summary>
/// Recent activity item
/// </summary>
public class ActivityDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string TypeIcon { get; set; } = string.Empty;
    public string TypeColor { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProjectName { get; set; }
    public Guid? ProjectId { get; set; }
    public string? User { get; set; }
    public DateTime Timestamp { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
}

/// <summary>
/// Settings DTO
/// </summary>
public class SettingsDto
{
    public ServerSettingsDto Server { get; set; } = new();
    public SyncSettingsDto Sync { get; set; } = new();
    public GitHubSettingsDto GitHub { get; set; } = new();
    public AiSettingsDto Ai { get; set; } = new();
    public NotificationSettingsDto Notifications { get; set; } = new();
    public BackupSettingsDto Backup { get; set; } = new();
    public SecuritySettingsDto Security { get; set; } = new();
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

public class ServerSettingsDto
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? ExternalUrl { get; set; }
    public bool UseHttps { get; set; }
    public string DataDirectory { get; set; } = "data";
}

public class GitHubSettingsDto
{
    public bool HasToken { get; set; }
    public string? TokenPreview { get; set; }
    public bool HasWebhookSecret { get; set; }
    public string? WebhookSecret { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public RateLimitInfoDto? RateLimit { get; set; }
}

public class RateLimitInfoDto
{
    public int Limit { get; set; }
    public int Remaining { get; set; }
    public string ResetsAt { get; set; } = string.Empty;
}

public class AiSettingsDto
{
    public bool Enabled { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string Provider { get; set; } = "Ollama";
    public string? OllamaEndpoint { get; set; }
    public string? OllamaModel { get; set; }
    public string? ClaudeModel { get; set; }
    public string? OpenAiModel { get; set; }
    public bool HasClaudeKey { get; set; }
    public bool HasOpenAiKey { get; set; }
    public bool BudgetEnabled { get; set; }
    public decimal MonthlyBudgetUsd { get; set; }
    public decimal CurrentUsageUsd { get; set; }
    // Advanced settings
    public AiModeInfo[] AvailableModes { get; set; } = [];
    public AiProviderSettingsDto Primary { get; set; } = new();
    public AiProviderSettingsDto? Fallback { get; set; }
    public decimal MonthlyBudget { get; set; }
    public int WarnAtPercent { get; set; }
    public bool StopAtLimit { get; set; }
    public bool RequireApproval { get; set; }
    public int MaxFilesPerFix { get; set; }
    public List<string> ExcludedPaths { get; set; } = [];
}

public class AiProviderSettingsDto
{
    public string Type { get; set; } = string.Empty;
    public bool HasApiKey { get; set; }
    public string? Endpoint { get; set; }
    public string? Model { get; set; }
}

public class AiModeInfo
{
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string RiskColor { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public class NotificationSettingsDto
{
    // Flat properties for simple settings API
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
    // Nested objects for detailed settings
    public EmailSettingsDto? Email { get; set; }
    public DiscordSettingsDto? Discord { get; set; }
    public TelegramSettingsDto? Telegram { get; set; }
    public LineOASettingsDto? LineOA { get; set; }
    public SlackSettingsDto? Slack { get; set; }
    public List<WebhookSettingsDto> Webhooks { get; set; } = [];
}

public class EmailSettingsDto
{
    public bool Enabled { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; }
    public bool UseSsl { get; set; }
    public string? FromAddress { get; set; }
    public bool HasCredentials { get; set; }
}

public class DiscordSettingsDto
{
    public bool Enabled { get; set; }
    public bool HasWebhookUrl { get; set; }
    public string? WebhookUrlPreview { get; set; }
}

public class TelegramSettingsDto
{
    public bool Enabled { get; set; }
    public bool HasBotToken { get; set; }
    public string? DefaultChatId { get; set; }
}

public class LineOASettingsDto
{
    public bool Enabled { get; set; }
    public bool HasChannelAccessToken { get; set; }
    public bool HasChannelSecret { get; set; }
}

public class SlackSettingsDto
{
    public bool Enabled { get; set; }
    public bool HasWebhookUrl { get; set; }
    public string? DefaultChannel { get; set; }
}

public class WebhookSettingsDto
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool HasSecret { get; set; }
    public List<string> Events { get; set; } = [];
}

public class BackupSettingsDto
{
    public string BasePath { get; set; } = string.Empty;
    public int DefaultMaxBackups { get; set; }
    public string CompressionType { get; set; } = string.Empty;
    public bool IncludeDatabase { get; set; }
    public string TotalSize { get; set; } = string.Empty;
    public int TotalCount { get; set; }
}

public class SecuritySettingsDto
{
    // Simple settings
    public int SessionTimeoutMinutes { get; set; } = 60;
    public bool Require2FaForAdmins { get; set; }
    public bool ApiEnabled { get; set; } = true;
    public int RateLimitPerMinute { get; set; } = 100;
    // Advanced settings
    public int JwtExpirationMinutes { get; set; }
    public int RefreshTokenExpirationDays { get; set; }
    public int MaxFailedLogins { get; set; }
    public int LockoutMinutes { get; set; }
    public bool RequireTwoFactor { get; set; }
    public List<string> AllowedOrigins { get; set; } = [];
}
