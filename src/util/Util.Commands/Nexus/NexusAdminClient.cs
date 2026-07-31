using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Util.Commands.Nexus;

/// <summary>
/// Administrative half of the Nexus REST API — repositories, realms, the admin password and the
/// NuGet API key. Deliberately separate from <see cref="NexusClient"/>, which is component-scoped
/// (enumerate / delete) and is what nexus-purge-repo uses.
///
/// Every call here is written to be idempotent by the caller (GET-then-POST/PUT), because the
/// provisioner runs on every AppHost start against a persistent /nexus-data volume.
/// </summary>
public sealed class NexusAdminClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;

    public NexusAdminClient(NexusOptions options)
    {
        _http = new HttpClient { BaseAddress = new Uri(options.Url.TrimEnd('/') + "/") };
        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{options.User}:{options.Password}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    /// <summary>
    /// Readiness probe. Deliberately static and UNAUTHENTICATED: /status is a public endpoint, but
    /// Nexus answers 401 when a request carries Basic credentials it does not accept — so probing
    /// with the configured password would report "not ready" for the entire bootstrap window, when
    /// the admin password is still the image's first-run default. Verified against a live instance:
    /// no auth => 200, wrong creds => 401, correct creds => 200.
    /// </summary>
    public static async Task<bool> IsUpAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var resp = await http.GetAsync($"{url.TrimEnd('/')}/service/rest/v1/status", ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// True when the configured credentials are accepted as an admin. Uses the realms endpoint
    /// because it requires administrative privileges, so a 200 proves more than /status would.
    /// </summary>
    public async Task<bool> CanAuthenticateAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync("service/rest/v1/security/realms/active", ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>Rotates the admin password. Body is text/plain, not JSON — this is Nexus' contract.</summary>
    public async Task ChangeAdminPasswordAsync(string user, string newPassword, CancellationToken ct = default)
    {
        using var content = new StringContent(newPassword, Encoding.UTF8, "text/plain");
        using var resp = await _http.PutAsync(
            $"service/rest/v1/security/users/{Uri.EscapeDataString(user)}/change-password", content, ct);
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>Names of all existing repositories.</summary>
    public async Task<HashSet<string>> GetRepositoryNamesAsync(CancellationToken ct = default)
    {
        var repos = await _http.GetFromJsonAsync<List<JsonNode>>("service/rest/v1/repositories", JsonOpts, ct)
                    ?? [];
        return repos
            .Select(r => r?["name"]?.GetValue<string>())
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Creates a hosted repository of the given format (nuget | raw | docker).</summary>
    public async Task CreateHostedRepositoryAsync(
        string format, JsonObject body, CancellationToken ct = default)
    {
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync($"service/rest/v1/repositories/{format}/hosted", content, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"create {format} repository failed ({(int)resp.StatusCode}): {detail}");
        }
    }

    /// <summary>Updates an existing hosted repository (used to fix a drifted docker httpPort).</summary>
    public async Task UpdateHostedRepositoryAsync(
        string format, string name, JsonObject body, CancellationToken ct = default)
    {
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var resp = await _http.PutAsync(
            $"service/rest/v1/repositories/{format}/hosted/{Uri.EscapeDataString(name)}", content, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"update {format} repository '{name}' failed ({(int)resp.StatusCode}): {detail}");
        }
    }

    /// <summary>
    /// The two settings on a docker hosted repo that CI depends on: the connector port images are
    /// pushed to, and whether re-pushing a tag is allowed. Null when the repo does not exist.
    /// </summary>
    public async Task<(int? HttpPort, string? WritePolicy)?> GetDockerRepoSettingsAsync(
        string name, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(
            $"service/rest/v1/repositories/docker/hosted/{Uri.EscapeDataString(name)}", ct);
        if (!resp.IsSuccessStatusCode) return null;

        var node = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
        return (node?["docker"]?["httpPort"]?.GetValue<int?>(),
                node?["storage"]?["writePolicy"]?.GetValue<string>());
    }

    public async Task<List<string>> GetActiveRealmsAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<string>>("service/rest/v1/security/realms/active", JsonOpts, ct) ?? [];

    public async Task SetActiveRealmsAsync(IEnumerable<string> realms, CancellationToken ct = default)
    {
        using var resp = await _http.PutAsJsonAsync("service/rest/v1/security/realms/active", realms, JsonOpts, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"set active realms failed ({(int)resp.StatusCode}): {detail}");
        }
    }

    /// <summary>
    /// EULA state. Returns null when the endpoint is absent (pre-Community-Edition builds), which the
    /// caller treats as "nothing to accept".
    /// </summary>
    public async Task<bool?> IsEulaAcceptedAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync("service/rest/v1/system/eula", ct);
        if (resp.StatusCode is HttpStatusCode.NotFound) return null;
        if (!resp.IsSuccessStatusCode) return null;

        var node = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
        return node?["accepted"]?.GetValue<bool>() ?? false;
    }

    public async Task AcceptEulaAsync(CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["accepted"] = true,
            ["disclaimer"] = "Use of Sonatype Nexus Repository - Community Edition is governed by the End User License Agreement"
        };
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("service/rest/v1/system/eula", content, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"accept EULA failed ({(int)resp.StatusCode}): {detail}");
        }
    }

    // No NuGet-API-key accessor here on purpose. Nexus 3.70.1 returns 404 for
    // service/rest/internal/ui/nuget-api-key, and the surviving service/rest/internal/nuget-api-key
    // demands a short-lived "authentication ticket" that base64(password) and base64(user:password)
    // both fail. Publishing uses basic auth instead — see jenkins/publish/nexus/nuget/Jenkinsfile.

    public void Dispose() => _http.Dispose();
}
