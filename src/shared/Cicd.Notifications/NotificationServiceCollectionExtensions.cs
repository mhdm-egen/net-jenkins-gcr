using Microsoft.Extensions.DependencyInjection;

namespace Cicd.Notifications;

public static class NotificationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the notification dispatcher + built-in senders (Slack, email). The caller binds
    /// <see cref="NotificationOptions"/> from its own config section, e.g.
    /// <c>services.Configure&lt;NotificationOptions&gt;(config.GetSection("Deployment:Notifications"))</c>.
    /// </summary>
    public static IServiceCollection AddCicdNotifications(this IServiceCollection services)
    {
        services.AddSingleton<INotificationSender, SlackNotificationSender>();
        services.AddSingleton<INotificationSender, SmtpEmailNotificationSender>();
        services.AddSingleton<INotificationDispatcher, NotificationDispatcher>();
        return services;
    }
}
