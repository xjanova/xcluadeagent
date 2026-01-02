using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MailKit.Net.Smtp;
using MimeKit;
using XcluadeAgent.Core.Enums;
using XcluadeAgent.Core.Interfaces;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.Notifications;

/// <summary>
/// Notification service implementation supporting multiple channels
/// </summary>
public class NotificationService : INotificationService
{
    private readonly NotificationConfig _config;
    private readonly ILogger<NotificationService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public NotificationService(
        IOptions<NotificationConfig> config,
        ILogger<NotificationService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();

        if (_config.Email?.Enabled == true)
            tasks.Add(SendEmailAsync(notification, cancellationToken));

        if (_config.Discord?.Enabled == true)
            tasks.Add(SendDiscordAsync(notification, cancellationToken));

        if (_config.Telegram?.Enabled == true)
            tasks.Add(SendTelegramAsync(notification, cancellationToken));

        if (_config.LineOA?.Enabled == true)
            tasks.Add(SendLineOAAsync(notification, cancellationToken));

        if (_config.Slack?.Enabled == true)
            tasks.Add(SendSlackAsync(notification, cancellationToken));

        foreach (var webhook in _config.Webhooks)
        {
            tasks.Add(SendWebhookAsync(webhook, notification, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    public async Task SendToChannelAsync(NotificationChannel channel, Notification notification, CancellationToken cancellationToken = default)
    {
        switch (channel)
        {
            case NotificationChannel.Email:
                await SendEmailAsync(notification, cancellationToken);
                break;
            case NotificationChannel.Discord:
                await SendDiscordAsync(notification, cancellationToken);
                break;
            case NotificationChannel.Telegram:
                await SendTelegramAsync(notification, cancellationToken);
                break;
            case NotificationChannel.LineOA:
                await SendLineOAAsync(notification, cancellationToken);
                break;
            case NotificationChannel.Slack:
                await SendSlackAsync(notification, cancellationToken);
                break;
        }
    }

    public async Task SendToUserAsync(Guid userId, Notification notification, CancellationToken cancellationToken = default)
    {
        // TODO: Get user notification preferences and send accordingly
        await SendAsync(notification, cancellationToken);
    }

    public async Task<bool> TestChannelAsync(NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        var testNotification = new Notification
        {
            Type = NotificationType.Info,
            Title = "🧪 Test Notification",
            Message = "This is a test notification from XcluadeAgent.",
            Details = $"Channel: {channel}\nTime: {DateTime.UtcNow:u}"
        };

        try
        {
            await SendToChannelAsync(channel, testNotification, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test notification failed for channel {Channel}", channel);
            return false;
        }
    }

    public IEnumerable<ChannelInfo> GetAvailableChannels()
    {
        yield return new ChannelInfo
        {
            Channel = NotificationChannel.Email,
            Name = "Email",
            Description = "Send notifications via SMTP email",
            IsConfigured = !string.IsNullOrEmpty(_config.Email?.SmtpHost),
            IsEnabled = _config.Email?.Enabled == true
        };

        yield return new ChannelInfo
        {
            Channel = NotificationChannel.Discord,
            Name = "Discord",
            Description = "Send notifications to Discord channel",
            IsConfigured = !string.IsNullOrEmpty(_config.Discord?.WebhookUrl),
            IsEnabled = _config.Discord?.Enabled == true
        };

        yield return new ChannelInfo
        {
            Channel = NotificationChannel.Telegram,
            Name = "Telegram",
            Description = "Send notifications via Telegram bot",
            IsConfigured = !string.IsNullOrEmpty(_config.Telegram?.BotToken),
            IsEnabled = _config.Telegram?.Enabled == true
        };

        yield return new ChannelInfo
        {
            Channel = NotificationChannel.LineOA,
            Name = "LINE Official Account",
            Description = "Send notifications via LINE Messaging API",
            IsConfigured = !string.IsNullOrEmpty(_config.LineOA?.ChannelAccessToken),
            IsEnabled = _config.LineOA?.Enabled == true
        };

        yield return new ChannelInfo
        {
            Channel = NotificationChannel.Slack,
            Name = "Slack",
            Description = "Send notifications to Slack channel",
            IsConfigured = !string.IsNullOrEmpty(_config.Slack?.WebhookUrl),
            IsEnabled = _config.Slack?.Enabled == true
        };
    }

    #region Email

    private async Task SendEmailAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (_config.Email == null) return;

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_config.Email.FromName ?? "XcluadeAgent", _config.Email.FromAddress));
            // TODO: Get recipient from notification or config
            // message.To.Add(new MailboxAddress("", "recipient@example.com"));

            message.Subject = notification.Title;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = BuildEmailHtml(notification),
                TextBody = BuildEmailText(notification)
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_config.Email.SmtpHost, _config.Email.SmtpPort, _config.Email.UseSsl, cancellationToken);

            if (!string.IsNullOrEmpty(_config.Email.Username))
            {
                await client.AuthenticateAsync(_config.Email.Username, _config.Email.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email notification sent: {Title}", notification.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification");
        }
    }

    private string BuildEmailHtml(Notification notification)
    {
        var color = GetNotificationColor(notification.Type);
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; border-radius: 8px; overflow: hidden; }}
        .header {{ background: {color}; color: white; padding: 20px; }}
        .content {{ padding: 20px; }}
        .footer {{ padding: 15px 20px; background: #f9f9f9; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2 style='margin: 0;'>{notification.Title}</h2>
        </div>
        <div class='content'>
            <p>{notification.Message}</p>
            {(string.IsNullOrEmpty(notification.Details) ? "" : $"<pre style='background: #f5f5f5; padding: 10px; border-radius: 4px;'>{notification.Details}</pre>")}
            {(string.IsNullOrEmpty(notification.ActionUrl) ? "" : $"<p><a href='{notification.ActionUrl}' style='background: {color}; color: white; padding: 10px 20px; text-decoration: none; border-radius: 4px;'>{notification.ActionText ?? "View Details"}</a></p>")}
        </div>
        <div class='footer'>
            XcluadeAgent by xman studio | <a href='https://xman4289.com'>xman4289.com</a>
        </div>
    </div>
</body>
</html>";
    }

    private string BuildEmailText(Notification notification)
    {
        var sb = new StringBuilder();
        sb.AppendLine(notification.Title);
        sb.AppendLine(new string('=', notification.Title.Length));
        sb.AppendLine();
        sb.AppendLine(notification.Message);

        if (!string.IsNullOrEmpty(notification.Details))
        {
            sb.AppendLine();
            sb.AppendLine(notification.Details);
        }

        if (!string.IsNullOrEmpty(notification.ActionUrl))
        {
            sb.AppendLine();
            sb.AppendLine($"Link: {notification.ActionUrl}");
        }

        return sb.ToString();
    }

    #endregion

    #region Discord

    private async Task SendDiscordAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (_config.Discord?.WebhookUrl == null) return;

        try
        {
            using var client = _httpClientFactory.CreateClient();

            var embed = new
            {
                title = notification.Title,
                description = notification.Message,
                color = GetDiscordColor(notification.Type),
                fields = string.IsNullOrEmpty(notification.Details) ? null : new[]
                {
                    new { name = "Details", value = TruncateForDiscord(notification.Details), inline = false }
                },
                footer = new { text = "XcluadeAgent | xman studio" },
                timestamp = notification.CreatedAt.ToString("o")
            };

            var payload = new
            {
                embeds = new[] { embed }
            };

            var response = await client.PostAsJsonAsync(_config.Discord.WebhookUrl, payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Discord notification sent: {Title}", notification.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Discord notification");
        }
    }

    private int GetDiscordColor(NotificationType type) => type switch
    {
        NotificationType.Success or NotificationType.SyncCompleted or NotificationType.RollbackCompleted => 0x2ECC71,
        NotificationType.Warning => 0xF39C12,
        NotificationType.Error or NotificationType.SyncFailed or NotificationType.RollbackFailed => 0xE74C3C,
        NotificationType.Info => 0x3498DB,
        _ => 0x95A5A6
    };

    private string TruncateForDiscord(string text, int maxLength = 1024)
    {
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }

    #endregion

    #region Telegram

    private async Task SendTelegramAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (_config.Telegram == null) return;

        try
        {
            using var client = _httpClientFactory.CreateClient();

            var emoji = GetNotificationEmoji(notification.Type);
            var message = $"{emoji} *{EscapeMarkdown(notification.Title)}*\n\n{EscapeMarkdown(notification.Message)}";

            if (!string.IsNullOrEmpty(notification.Details))
            {
                message += $"\n\n```\n{notification.Details}\n```";
            }

            var payload = new
            {
                chat_id = _config.Telegram.DefaultChatId,
                text = message,
                parse_mode = "Markdown"
            };

            var response = await client.PostAsJsonAsync(
                $"https://api.telegram.org/bot{_config.Telegram.BotToken}/sendMessage",
                payload,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Telegram notification sent: {Title}", notification.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram notification");
        }
    }

    private string EscapeMarkdown(string text)
    {
        return text
            .Replace("_", "\\_")
            .Replace("*", "\\*")
            .Replace("[", "\\[")
            .Replace("`", "\\`");
    }

    #endregion

    #region LINE Official Account

    private async Task SendLineOAAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (_config.LineOA == null) return;

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.LineOA.ChannelAccessToken}");

            var emoji = GetNotificationEmoji(notification.Type);
            var message = new
            {
                to = _config.LineOA.DefaultUserId,
                messages = new object[]
                {
                    new
                    {
                        type = "flex",
                        altText = notification.Title,
                        contents = new
                        {
                            type = "bubble",
                            header = new
                            {
                                type = "box",
                                layout = "vertical",
                                backgroundColor = GetLineColor(notification.Type),
                                contents = new[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = $"{emoji} {notification.Title}",
                                        color = "#FFFFFF",
                                        weight = "bold",
                                        size = "md"
                                    }
                                }
                            },
                            body = new
                            {
                                type = "box",
                                layout = "vertical",
                                contents = BuildLineBodyContents(notification)
                            },
                            footer = new
                            {
                                type = "box",
                                layout = "vertical",
                                contents = new[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = "XcluadeAgent | xman studio",
                                        size = "xs",
                                        color = "#AAAAAA",
                                        align = "center"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var response = await client.PostAsJsonAsync("https://api.line.me/v2/bot/message/push", message, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("LINE OA notification sent: {Title}", notification.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send LINE OA notification");
        }
    }

    private object[] BuildLineBodyContents(Notification notification)
    {
        var contents = new List<object>
        {
            new { type = "text", text = notification.Message, wrap = true, size = "sm" }
        };

        if (!string.IsNullOrEmpty(notification.Details))
        {
            contents.Add(new { type = "separator", margin = "md" });
            contents.Add(new { type = "text", text = notification.Details, wrap = true, size = "xs", color = "#666666" });
        }

        return contents.ToArray();
    }

    private string GetLineColor(NotificationType type) => type switch
    {
        NotificationType.Success or NotificationType.SyncCompleted or NotificationType.RollbackCompleted => "#27AE60",
        NotificationType.Warning => "#F39C12",
        NotificationType.Error or NotificationType.SyncFailed or NotificationType.RollbackFailed => "#E74C3C",
        NotificationType.Info => "#3498DB",
        _ => "#7F8C8D"
    };

    #endregion

    #region Slack

    private async Task SendSlackAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (_config.Slack?.WebhookUrl == null) return;

        try
        {
            using var client = _httpClientFactory.CreateClient();

            var emoji = GetNotificationEmoji(notification.Type);
            var payload = new
            {
                blocks = new object[]
                {
                    new
                    {
                        type = "header",
                        text = new { type = "plain_text", text = $"{emoji} {notification.Title}" }
                    },
                    new
                    {
                        type = "section",
                        text = new { type = "mrkdwn", text = notification.Message }
                    }
                }
            };

            var response = await client.PostAsJsonAsync(_config.Slack.WebhookUrl, payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Slack notification sent: {Title}", notification.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack notification");
        }
    }

    #endregion

    #region Webhook

    private async Task SendWebhookAsync(WebhookConfig webhook, Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();

            var payload = new
            {
                id = notification.Id,
                type = notification.Type.ToString(),
                title = notification.Title,
                message = notification.Message,
                details = notification.Details,
                projectId = notification.ProjectId,
                projectName = notification.ProjectName,
                timestamp = notification.CreatedAt
            };

            var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrEmpty(webhook.Secret))
            {
                var signature = ComputeHmacSha256(JsonSerializer.Serialize(payload), webhook.Secret);
                request.Headers.Add("X-Webhook-Signature", signature);
            }

            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Webhook notification sent to {Name}: {Title}", webhook.Name, notification.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send webhook notification to {Name}", webhook.Name);
        }
    }

    private string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return $"sha256={Convert.ToHexString(hash).ToLower()}";
    }

    #endregion

    #region Helpers

    private string GetNotificationColor(NotificationType type) => type switch
    {
        NotificationType.Success or NotificationType.SyncCompleted or NotificationType.RollbackCompleted => "#27AE60",
        NotificationType.Warning => "#F39C12",
        NotificationType.Error or NotificationType.SyncFailed or NotificationType.RollbackFailed => "#E74C3C",
        NotificationType.Info => "#3498DB",
        _ => "#7F8C8D"
    };

    private string GetNotificationEmoji(NotificationType type) => type switch
    {
        NotificationType.Success or NotificationType.SyncCompleted => "✅",
        NotificationType.RollbackCompleted => "⏪",
        NotificationType.Warning => "⚠️",
        NotificationType.Error or NotificationType.SyncFailed or NotificationType.RollbackFailed => "❌",
        NotificationType.Info => "ℹ️",
        NotificationType.HealthCheckFailed => "🚨",
        NotificationType.AiSuggestion or NotificationType.AiAnalysis => "🤖",
        NotificationType.AiApprovalRequired => "🔔",
        _ => "📢"
    };

    #endregion
}
