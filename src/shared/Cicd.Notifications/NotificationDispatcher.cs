using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cicd.Notifications;

/// <summary>Fans a message out to every enabled channel. Delivery is best-effort — a channel
/// failure is logged, never thrown, so notifications can't break the operation that triggered them.</summary>
public interface INotificationDispatcher
{
    Task NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}

internal sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly IEnumerable<INotificationSender> _senders;
    private readonly IOptionsMonitor<NotificationOptions> _options;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IEnumerable<INotificationSender> senders,
        IOptionsMonitor<NotificationOptions> options,
        ILogger<NotificationDispatcher> logger)
    {
        _senders = senders;
        _options = options;
        _logger = logger;
    }

    public async Task NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (_options.CurrentValue.OnlyFailures && message.Severity != NotificationSeverity.Failure)
            return;

        foreach (var sender in _senders)
        {
            if (!sender.Enabled) continue;
            try
            {
                await sender.SendAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[notify] {Channel} delivery failed for '{Title}'", sender.Channel, message.Title);
            }
        }
    }
}
