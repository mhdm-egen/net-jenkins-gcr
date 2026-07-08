namespace Cicd.Notifications;

/// <summary>One delivery channel (Slack, email, …). The dispatcher fans a message out to every
/// enabled sender; a sender that isn't configured reports <see cref="Enabled"/> = false.</summary>
public interface INotificationSender
{
    string Channel { get; }
    bool Enabled { get; }
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}
