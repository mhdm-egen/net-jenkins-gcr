namespace Metering.Application.Abstractions;

/// <summary>
/// Turns a metered quantity into a cost snapshot against a versioned rate table. Cost is
/// computed at ingest and stored on each <see cref="Domain.UsageRecord"/> (with the
/// <see cref="Version"/>) so history can be repriced later.
/// </summary>
public interface IUsageRater
{
    /// <summary>Rate-table version stamped onto each rated record.</summary>
    string Version { get; }

    /// <summary>USD cost for <paramref name="tokens"/> of an AI token direction on a model.</summary>
    decimal RateAiTokens(string model, string direction, long tokens);
}
