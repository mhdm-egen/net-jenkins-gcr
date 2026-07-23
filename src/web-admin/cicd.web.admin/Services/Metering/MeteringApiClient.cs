using System.Net.Http.Json;
using Metering.Contracts.Usage;

namespace Cicd.Web.Admin.Services.Metering;

/// <summary>
/// Read-side typed client for the metering-api usage rollups (the AI Usage page).
/// Separate from the fire-and-forget ingest recorder. No-ops when unconfigured.
/// </summary>
public sealed class MeteringApiClient
{
    private readonly HttpClient _http;
    private readonly MeteringApiOptions _options;

    public MeteringApiClient(HttpClient http, MeteringApiOptions options)
    {
        _http = http;
        _options = options;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public async Task<UsageSummaryDto?> GetUsageSummaryAsync(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        var query = new List<string>();
        if (fromUtc is { } f) query.Add($"fromUtc={Uri.EscapeDataString(f.ToString("O"))}");
        if (toUtc is { } t) query.Add($"toUtc={Uri.EscapeDataString(t.ToString("O"))}");
        var url = "api/metering/usage/summary" + (query.Count > 0 ? "?" + string.Join("&", query) : "");

        return await _http.GetFromJsonAsync<UsageSummaryDto>(url, ct);
    }

    public async Task<IReadOnlyList<MeterTotalDto>?> GetMeterTotalsAsync(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        var query = new List<string>();
        if (fromUtc is { } f) query.Add($"fromUtc={Uri.EscapeDataString(f.ToString("O"))}");
        if (toUtc is { } t) query.Add($"toUtc={Uri.EscapeDataString(t.ToString("O"))}");
        var url = "api/metering/usage/meters" + (query.Count > 0 ? "?" + string.Join("&", query) : "");

        return await _http.GetFromJsonAsync<List<MeterTotalDto>>(url, ct);
    }
}
