namespace XcluadeAgent.Shared.Constants;

/// <summary>
/// Application constants
/// </summary>
public static class AppConstants
{
    public const string AppName = "XcluadeAgent";
    public const string Developer = "xman studio";
    public const string Website = "https://xman4289.com";
    public const string GitHubRepo = "https://github.com/xjanova/xmanstudio";

    public static class Versions
    {
        public const string Current = "1.0.0";
        public const string MinimumConfig = "1.0.0";
    }

    public static class Defaults
    {
        public const int Port = 5000;
        public const string Host = "0.0.0.0";
        public const string DatabaseProvider = "sqlite";
        public const string ConfigFileName = "config.yaml";
        public const string DataFolder = "data";
        public const string LogsFolder = "logs";
        public const string BackupsFolder = "backups";
    }

    public static class Security
    {
        public const int MinPasswordLength = 8;
        public const int MaxFailedLogins = 5;
        public const int LockoutMinutes = 15;
        public const int JwtExpirationMinutes = 1440; // 24 hours
        public const int RefreshTokenDays = 30;
    }

    public static class Limits
    {
        public const int MaxProjectNameLength = 100;
        public const int MaxDescriptionLength = 500;
        public const int MaxPathLength = 1000;
        public const int MaxCommandLength = 1000;
        public const int MaxBackupsDefault = 5;
        public const int MaxFilesPerSync = 10000;
        public const long MaxFileSizeBytes = 100 * 1024 * 1024; // 100MB
    }

    public static class Cache
    {
        public const int GitHubRateLimitSeconds = 60;
        public const int ProjectStatusSeconds = 30;
        public const int DashboardSeconds = 60;
    }

    public static class Api
    {
        public const string Version = "v1";
        public const string BaseRoute = "/api/v1";
    }
}

/// <summary>
/// Status colors for UI
/// </summary>
public static class StatusColors
{
    public const string Success = "green";
    public const string Warning = "yellow";
    public const string Error = "red";
    public const string Info = "blue";
    public const string Pending = "orange";
    public const string Disabled = "gray";
}

/// <summary>
/// Event names for SignalR/Webhooks
/// </summary>
public static class Events
{
    public const string SyncStarted = "sync.started";
    public const string SyncProgress = "sync.progress";
    public const string SyncCompleted = "sync.completed";
    public const string SyncFailed = "sync.failed";

    public const string RollbackStarted = "rollback.started";
    public const string RollbackCompleted = "rollback.completed";
    public const string RollbackFailed = "rollback.failed";

    public const string ProjectCreated = "project.created";
    public const string ProjectUpdated = "project.updated";
    public const string ProjectDeleted = "project.deleted";

    public const string AiAnalysisStarted = "ai.analysis.started";
    public const string AiAnalysisCompleted = "ai.analysis.completed";
    public const string AiFixSuggested = "ai.fix.suggested";
    public const string AiFixApprovalRequired = "ai.fix.approval_required";
    public const string AiFixApplied = "ai.fix.applied";

    public const string HealthCheckFailed = "health.check.failed";
    public const string HealthCheckRecovered = "health.check.recovered";

    public const string SystemAlert = "system.alert";
}

/// <summary>
/// Claim types for JWT
/// </summary>
public static class ClaimTypes
{
    public const string UserId = "uid";
    public const string Username = "username";
    public const string Role = "role";
    public const string DisplayName = "display_name";
    public const string Email = "email";
}
