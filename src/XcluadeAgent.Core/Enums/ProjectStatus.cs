namespace XcluadeAgent.Core.Enums;

/// <summary>
/// Project sync status
/// </summary>
public enum ProjectStatus
{
    Unknown = 0,
    Synced = 1,
    Pending = 2,
    Syncing = 3,
    Failed = 4,
    RollingBack = 5,
    Disabled = 6
}

/// <summary>
/// Sync trigger type
/// </summary>
public enum SyncTrigger
{
    Manual = 0,
    Webhook = 1,
    Scheduled = 2,
    AutoRollback = 3
}

/// <summary>
/// Framework type for auto-detection
/// </summary>
public enum FrameworkType
{
    Unknown = 0,
    Static = 1,
    Laravel = 2,
    Symfony = 3,
    CodeIgniter = 4,
    WordPress = 5,
    NodeJs = 6,
    React = 7,
    Vue = 8,
    Nuxt = 9,
    NextJs = 10,
    Angular = 11,
    Django = 12,
    Flask = 13,
    FastApi = 14,
    Rails = 15,
    DotNet = 16,
    Go = 17,
    Rust = 18,
    Other = 50,
    Custom = 99
}

/// <summary>
/// Notification channel type
/// </summary>
public enum NotificationChannel
{
    None = 0,
    Email = 1,
    Discord = 2,
    Telegram = 3,
    LineOA = 4,
    Slack = 5,
    Webhook = 6
}

/// <summary>
/// User role in the system
/// </summary>
public enum UserRole
{
    Viewer = 0,
    Operator = 1,
    Admin = 2,
    SuperAdmin = 3
}

/// <summary>
/// License type
/// </summary>
public enum LicenseType
{
    Community = 0,
    Professional = 1,
    Enterprise = 2,
    Unlimited = 3
}

/// <summary>
/// AI provider type
/// </summary>
public enum AiProvider
{
    None = 0,
    Ollama = 1,
    Claude = 2,
    OpenAi = 3,
    Custom = 4
}

/// <summary>
/// Extension methods for FrameworkType
/// </summary>
public static class FrameworkTypeExtensions
{
    public static string GetDisplayName(this FrameworkType type) => type switch
    {
        FrameworkType.Unknown => "Unknown",
        FrameworkType.Static => "Static HTML",
        FrameworkType.Laravel => "Laravel (PHP)",
        FrameworkType.Symfony => "Symfony (PHP)",
        FrameworkType.CodeIgniter => "CodeIgniter (PHP)",
        FrameworkType.WordPress => "WordPress",
        FrameworkType.NodeJs => "Node.js",
        FrameworkType.React => "React",
        FrameworkType.Vue => "Vue.js",
        FrameworkType.Nuxt => "Nuxt.js",
        FrameworkType.NextJs => "Next.js",
        FrameworkType.Angular => "Angular",
        FrameworkType.Django => "Django (Python)",
        FrameworkType.Flask => "Flask (Python)",
        FrameworkType.FastApi => "FastAPI (Python)",
        FrameworkType.Rails => "Ruby on Rails",
        FrameworkType.DotNet => ".NET",
        FrameworkType.Go => "Go",
        FrameworkType.Rust => "Rust",
        FrameworkType.Other => "Other",
        FrameworkType.Custom => "Custom",
        _ => "Unknown"
    };

    public static string[] GetDefaultPostDeployCommands(this FrameworkType type) => type switch
    {
        FrameworkType.Laravel => [
            "composer install --no-dev --optimize-autoloader",
            "php artisan migrate --force",
            "php artisan config:cache",
            "php artisan route:cache",
            "php artisan view:cache"
        ],
        FrameworkType.Symfony => [
            "composer install --no-dev --optimize-autoloader",
            "php bin/console cache:clear --env=prod",
            "php bin/console doctrine:migrations:migrate --no-interaction"
        ],
        FrameworkType.NodeJs => [
            "npm ci --production",
            "npm run build"
        ],
        FrameworkType.React => [
            "npm ci",
            "npm run build"
        ],
        FrameworkType.Vue => [
            "npm ci",
            "npm run build"
        ],
        FrameworkType.Nuxt => [
            "npm ci",
            "npm run generate"
        ],
        FrameworkType.NextJs => [
            "npm ci",
            "npm run build"
        ],
        FrameworkType.Django => [
            "pip install -r requirements.txt",
            "python manage.py migrate",
            "python manage.py collectstatic --noinput"
        ],
        FrameworkType.Flask => [
            "pip install -r requirements.txt"
        ],
        FrameworkType.FastApi => [
            "pip install -r requirements.txt"
        ],
        FrameworkType.DotNet => [
            "dotnet restore",
            "dotnet build -c Release"
        ],
        FrameworkType.WordPress => [
            "# WordPress typically doesn't need build commands"
        ],
        _ => []
    };
}
