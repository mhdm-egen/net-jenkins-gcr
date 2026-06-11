namespace Cicd.Messaging;

/// <summary>
/// Transport-neutral declaration of a service's cross-service messaging: which integration
/// event types it <see cref="Publish{TEvent}"/>es to a logical channel, and which channels
/// it <see cref="Subscribe"/>s to. A per-provider mapping (see <c>CicdMessaging</c>)
/// translates each logical channel to the broker's primitives (RabbitMQ exchange/queue,
/// Service Bus / Pub/Sub topic+subscription, …).
/// </summary>
public sealed class MessagingTopology
{
    internal List<Publication> Publications { get; } = new();
    internal List<Subscription> Subscriptions { get; } = new();

    /// <summary>Route an integration event type to a logical channel (a fan-out destination).</summary>
    public MessagingTopology Publish<TEvent>(string channel)
    {
        Publications.Add(new Publication(typeof(TEvent), channel));
        return this;
    }

    /// <summary>
    /// Subscribe this service to a logical channel. <paramref name="subscriber"/> names this
    /// service so the underlying queue/subscription is unique per consumer (independent fan-out).
    /// </summary>
    public MessagingTopology Subscribe(string channel, string subscriber)
    {
        Subscriptions.Add(new Subscription(channel, subscriber));
        return this;
    }

    internal sealed record Publication(Type EventType, string Channel);
    internal sealed record Subscription(string Channel, string Subscriber);
}
