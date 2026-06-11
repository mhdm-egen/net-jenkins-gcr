using Microsoft.Extensions.Configuration;
using Wolverine;
using Wolverine.RabbitMQ;

namespace Cicd.Messaging;

/// <summary>
/// Provider-pluggable cross-service messaging for the cicd platform. Application code stays
/// transport-agnostic (it publishes via <c>IMessageBus</c> and consumes via Wolverine
/// handlers); this seam is the only place a broker is chosen. The provider is selected by
/// the <c>Messaging:Provider</c> config key; the default is RabbitMQ.
///
/// Supported in this cut: <c>Rabbit</c> (default) and <c>InMemory</c> (tests / single-process).
/// <c>AzureServiceBus</c> / <c>GcpPubSub</c> / … slot in as additional cases without touching
/// publish/consume code. Portable semantics are at-least-once + idempotent consumers.
/// </summary>
public static class CicdMessaging
{
    public const string ProviderKey = "Messaging:Provider";
    public const string ConnectionStringName = "messaging";

    public static WolverineOptions AddCicdMessaging(
        this WolverineOptions opts,
        IConfiguration configuration,
        Action<MessagingTopology> configureTopology)
    {
        var topology = new MessagingTopology();
        configureTopology(topology);

        var provider = (configuration[ProviderKey] ?? "Rabbit").Trim();
        switch (provider.ToLowerInvariant())
        {
            case "inmemory":
                // Single-process / tests: no external transport. Wolverine's local queues
                // carry the messages; publications and subscriptions resolve in-memory.
                break;

            case "rabbit":
                ConfigureRabbit(opts, configuration, topology);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported '{ProviderKey}' value '{provider}'. Supported in this build: Rabbit, InMemory.");
        }

        return opts;
    }

    private static void ConfigureRabbit(WolverineOptions opts, IConfiguration configuration, MessagingTopology topology)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is required for the Rabbit messaging provider.");

        var rabbit = opts.UseRabbitMq(new Uri(connectionString)).AutoProvision();

        // Each logical channel is a fan-out exchange; each subscriber gets its own queue
        // bound to it, so independent services receive every message (true pub/sub).
        var channels = topology.Publications.Select(p => p.Channel)
            .Concat(topology.Subscriptions.Select(s => s.Channel))
            .Distinct(StringComparer.Ordinal);
        foreach (var channel in channels)
        {
            rabbit.DeclareExchange(channel, ex => ex.ExchangeType = ExchangeType.Fanout);
        }

        foreach (var pub in topology.Publications)
        {
            opts.Publish(x => x.Message(pub.EventType).ToRabbitExchange(pub.Channel));
        }

        foreach (var sub in topology.Subscriptions)
        {
            var queue = $"{sub.Channel}.{sub.Subscriber}";
            rabbit.BindExchange(sub.Channel).ToQueue(queue);
            opts.ListenToRabbitQueue(queue);
        }
    }
}
