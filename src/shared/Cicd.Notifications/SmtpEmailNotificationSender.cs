using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Cicd.Notifications;

/// <summary>Sends the notification as a plain-text email over SMTP. Credentials are optional
/// (anonymous relays / localhost); UseSsl toggles STARTTLS.</summary>
internal sealed class SmtpEmailNotificationSender : INotificationSender
{
    private readonly IOptionsMonitor<NotificationOptions> _options;

    public SmtpEmailNotificationSender(IOptionsMonitor<NotificationOptions> options) => _options = options;

    public string Channel => "email";
    public bool Enabled => _options.CurrentValue.Email.IsUsable;

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var email = _options.CurrentValue.Email;
        if (!email.IsUsable) return;

        var body = string.Join(Environment.NewLine, message.Fields);
        if (!string.IsNullOrWhiteSpace(message.Link))
            body += Environment.NewLine + Environment.NewLine + message.Link;

        using var mail = new MailMessage { From = new MailAddress(email.From), Subject = message.Title, Body = body };
        foreach (var to in email.To) mail.To.Add(to);

        using var client = new SmtpClient(email.SmtpHost, email.Port) { EnableSsl = email.UseSsl };
        if (!string.IsNullOrWhiteSpace(email.Username))
            client.Credentials = new NetworkCredential(email.Username, email.Password);

        await client.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
    }
}
