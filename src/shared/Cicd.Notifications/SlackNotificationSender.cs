using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Cicd.Notifications;

/// <summary>Posts to a Slack Incoming Webhook. Renders a coloured attachment (green/red/grey by
/// severity) with the title, the fields as text, and an optional link.</summary>
internal sealed class SlackNotificationSender : INotificationSender
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly IOptionsMonitor<NotificationOptions> _options;

    public SlackNotificationSender(IOptionsMonitor<NotificationOptions> options) => _options = options;

    public string Channel => "slack";
    public bool Enabled => _options.CurrentValue.Slack.IsUsable;

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var slack = _options.CurrentValue.Slack;
        if (!slack.IsUsable) return;

        var text = new System.Text.StringBuilder(message.Title);
        foreach (var f in message.Fields) text.Append('\n').Append(f);
        if (!string.IsNullOrWhiteSpace(message.Link)) text.Append('\n').Append('<').Append(message.Link).Append("|details>");

        var payload = new
        {
            attachments = new[]
            {
                new { color = Color(message.Severity), text = text.ToString(), fallback = message.Title },
            },
        };

        using var resp = await Http.PostAsJsonAsync(slack.WebhookUrl, payload, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    private static string Color(NotificationSeverity s) => s switch
    {
        NotificationSeverity.Success => "#2eb67d",
        NotificationSeverity.Failure => "#e01e5a",
        _                            => "#8d8d8d",
    };
}
