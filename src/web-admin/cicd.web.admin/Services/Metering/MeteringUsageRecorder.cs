using System.Net.Http.Json;
using Cicd.Web.Admin.Services.Ai;
using Metering.Contracts.Usage;

namespace Cicd.Web.Admin.Services.Metering;

/// <summary>
/// Ships AI token usage to the metering-api ledger via fire-and-forget HTTP — web-admin
/// is a Blazor UI host, not a bus participant, so it POSTs rather than publishing an
/// integration event. The POST never blocks or fails the user's AI call: errors are
/// swallowed and logged. When BaseUrl is unconfigured this is a no-op.
/// </summary>
public sealed class MeteringUsageRecorder : IAiUsageRecorder
{
    public const string HttpClientName = "metering";

    private readonly IHttpClientFactory _factory;
    private readonly MeteringApiOptions _options;
    private readonly ILogger<MeteringUsageRecorder> _log;

    public MeteringUsageRecorder(
        IHttpClientFactory factory,
        MeteringApiOptions options,
        ILogger<MeteringUsageRecorder> log)
    {
        _factory = factory;
        _options = options;
        _log = log;
    }

    public void Record(
        string feature,
        string model,
        AiUsage usage,
        IReadOnlyDictionary<string, string>? dimensions)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl)) return; // metering not wired up

        string? Dim(string key) =>
            dimensions is not null && dimensions.TryGetValue(key, out var v) ? v : null;

        var request = new IngestAiUsageRequest(
            EventId: Guid.NewGuid(),
            Feature: feature,
            Model: model,
            Source: "web-admin",
            InputTokens: usage.InputTokens,
            OutputTokens: usage.OutputTokens,
            CacheReadTokens: usage.CacheReadInputTokens,
            CacheWriteTokens: usage.CacheCreationInputTokens,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            Repository: Dim("repository"),
            Service: Dim("service"),
            Environment: Dim("environment"));

        _ = PostAsync(request);
    }

    private async Task PostAsync(IngestAiUsageRequest request)
    {
        try
        {
            var client = _factory.CreateClient(HttpClientName);
            using var resp = await client.PostAsJsonAsync("api/metering/usage/ai", request);
            if (!resp.IsSuccessStatusCode)
                _log.LogWarning("Metering ingest returned {Status} for event {EventId}", resp.StatusCode, request.EventId);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Metering ingest failed for event {EventId} (usage not recorded to ledger)", request.EventId);
        }
    }
}
