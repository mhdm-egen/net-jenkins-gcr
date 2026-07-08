namespace Cicd.Notifications;

/// <summary>
/// Notification configuration. The host binds this from its own config section (e.g.
/// <c>Deployment:Notifications</c>). Every channel is opt-in — nothing sends until a channel is
/// enabled and configured, so this is safe to leave absent.
/// </summary>
public sealed class NotificationOptions
{
    /// <summary>When true, only <see cref="NotificationSeverity.Failure"/> messages are sent.</summary>
    public bool OnlyFailures { get; set; }

    public SlackOptions Slack { get; set; } = new();
    public EmailOptions Email { get; set; } = new();

    public sealed class SlackOptions
    {
        public bool Enabled { get; set; }
        /// <summary>Slack Incoming Webhook URL.</summary>
        public string WebhookUrl { get; set; } = string.Empty;

        public bool IsUsable => Enabled && !string.IsNullOrWhiteSpace(WebhookUrl);
    }

    public sealed class EmailOptions
    {
        public bool Enabled { get; set; }
        public string SmtpHost { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string From { get; set; } = string.Empty;
        public List<string> To { get; set; } = new();

        public bool IsUsable => Enabled
            && !string.IsNullOrWhiteSpace(SmtpHost)
            && !string.IsNullOrWhiteSpace(From)
            && To.Count > 0;
    }
}
