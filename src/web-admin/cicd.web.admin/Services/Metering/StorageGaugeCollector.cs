using System.Net.Http.Json;
using Cicd.Ai;
using Cicd.Web.Admin.Services.Nexus;
using Metering.Contracts.Usage;

namespace Cicd.Web.Admin.Services.Metering;

public sealed class StorageGaugeOptions
{
    public const string SectionName = "Metering:Gauges";

    /// <summary>
    /// On by default, but the collector still does nothing unless Nexus is configured — so a
    /// standalone web-admin with no Nexus stays silent rather than logging failures every cycle.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Storage moves slowly and the Nexus listing walks every asset, so this is deliberately long.
    /// A gauge is a level: sampling it more often produces more rows, not more insight.
    /// </summary>
    public int IntervalMinutes { get; set; } = 360;

    /// <summary>
    /// Delay before the first sample. The collector starts with the host, and racing it against
    /// Nexus's own startup just produces a failed first cycle.
    /// </summary>
    public int StartupDelaySeconds { get; set; } = 60;
}

/// <summary>
/// The scheduled gauge collector the metering design reserved from the start —
/// <c>MeterType.Gauge</c> and the storage <c>MeterKind</c> values have existed unfed since the
/// ledger was written, and both this and the Aspire host's Redis comment name it explicitly.
///
/// It samples what the platform can actually measure accurately and posts each as a gauge:
/// <list type="bullet">
///   <item><c>NexusStorage</c> — summed <c>SizeBytes</c> over the NuGet repository's packages.</item>
///   <item><c>DockerStorage</c> — summed asset sizes over the Docker repository, from the ASSET
///   listing rather than the image listing. <c>DockerImage.SizeBytes</c> is documented as mostly
///   the manifest, with shared blob layers living as separate components it does not count, so
///   summing images would under-report badly. Assets include the blobs.</item>
/// </list>
///
/// It runs in web-admin rather than metering-api because that is where <see cref="INexusClient"/>
/// lives; metering-api has no Nexus client, no HTTP client and no credentials. Moving the client
/// would be a bigger change than the meter is worth, and this keeps metering-api a pure ledger.
/// </summary>
public sealed class StorageGaugeCollector : BackgroundService
{
    // Scoped services resolved per cycle through the factory, NOT injected. A BackgroundService is
    // a singleton, so holding a scoped dependency is a captive dependency and ValidateScopes
    // refuses to build the host — which fails startup, not the feature.
    private readonly IServiceScopeFactory _scopes;
    private readonly StorageGaugeOptions _options;
    private readonly MeteringApiOptions _metering;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<StorageGaugeCollector> _log;

    public StorageGaugeCollector(
        IServiceScopeFactory scopes,
        StorageGaugeOptions options,
        MeteringApiOptions metering,
        IHttpClientFactory http,
        ILogger<StorageGaugeCollector> log)
    {
        _scopes = scopes;
        _options = options;
        _metering = metering;
        _http = http;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            _log.LogInformation("Storage gauge collector is disabled (Metering:Gauges:Enabled=false).");
            return;
        }

        if (string.IsNullOrWhiteSpace(_metering.BaseUrl))
        {
            _log.LogInformation("Storage gauge collector idle: no metering API configured.");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.StartupDelaySeconds), ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await CollectAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // A collector that dies on one bad cycle stops metering silently, which is worse
                // than a gap in the series. Log and wait for the next tick.
                _log.LogWarning(ex, "Storage gauge collection failed; will retry next cycle.");
            }

            try
            {
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task CollectAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var nexus = scope.ServiceProvider.GetRequiredService<INexusClient>();

        if (!nexus.IsConfigured)
        {
            _log.LogDebug("Storage gauge collection skipped: Nexus is not configured.");
            return;
        }

        var sampledAt = DateTimeOffset.UtcNow;

        var packages = await nexus.ListNuGetPackagesAsync(ct);
        var nugetBytes = packages.Sum(p => p.SizeBytes ?? 0L);
        await PostGaugeAsync("NexusStorage", nugetBytes, "byte", nexus.NuGetRepositoryName, sampledAt, ct);

        // Distinct by path: the asset listing can repeat a shared blob across manifests, and
        // counting it twice would overstate what is actually stored.
        var assets = await nexus.ListDockerAssetsAsync(ct);
        var dockerBytes = assets
            .GroupBy(a => a.Path, StringComparer.Ordinal)
            .Sum(g => g.First().FileSize ?? 0L);
        await PostGaugeAsync("DockerStorage", dockerBytes, "byte", nexus.DockerRepositoryName, sampledAt, ct);

        _log.LogInformation(
            "Storage gauges sampled: {NuGetRepo}={NuGetBytes} bytes over {PackageCount} packages, " +
            "{DockerRepo}={DockerBytes} bytes over {AssetCount} assets.",
            nexus.NuGetRepositoryName, nugetBytes, packages.Count,
            nexus.DockerRepositoryName, dockerBytes, assets.Count);
    }

    private async Task PostGaugeAsync(
        string meter, double quantity, string unit, string repository,
        DateTimeOffset sampledAt, CancellationToken ct)
    {
        var request = new IngestGaugeRequest(
            EventId: Guid.NewGuid(),
            Meter: meter,
            Quantity: quantity,
            Unit: unit,
            OccurredAtUtc: sampledAt,
            Source: "web-admin",
            Repository: repository);

        // Reuses the named client the AI usage recorder already configures with the metering base
        // address, rather than a second one that could drift from it.
        var client = _http.CreateClient(Cicd.Ai.MeteringUsageRecorder.HttpClientName);
        using var resp = await client.PostAsJsonAsync("api/metering/usage/gauge", request, ct);

        if (!resp.IsSuccessStatusCode)
        {
            _log.LogWarning("Gauge ingest for {Meter} returned {Status}", meter, resp.StatusCode);
        }
    }
}
