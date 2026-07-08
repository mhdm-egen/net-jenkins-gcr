namespace Cicd.Notifications;

public enum NotificationSeverity
{
    Success,
    Failure,
    Info,
}

/// <summary>
/// A transport-agnostic notification. Senders render it to their channel's format (Slack blocks,
/// an email body, …). <see cref="Fields"/> are short "Label: value" lines shown under the title.
/// </summary>
public sealed record NotificationMessage(
    string Title,
    NotificationSeverity Severity,
    IReadOnlyList<string> Fields,
    string? Link = null);
