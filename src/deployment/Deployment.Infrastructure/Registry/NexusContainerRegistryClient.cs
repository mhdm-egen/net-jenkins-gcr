using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Deployment.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Deployment.Infrastructure.Registry;

/// <summary>
/// <see cref="IContainerImageResolver"/> over the Docker Registry v2 HTTP API (Nexus is
/// the system of record). Resolves a tag to its content digest from the
/// <c>Docker-Content-Digest</c> response header, and lists available tags from
/// <c>/v2/{repo}/{name}/tags/list</c>. Auth is optional HTTP basic.
/// </summary>
internal sealed class NexusContainerRegistryClient : IContainerImageResolver
{
    // Manifest media types we accept; multi-arch indexes first so a digest header is
    // returned for the index rather than a single platform manifest.
    private static readonly string[] ManifestAccept =
    {
        "application/vnd.oci.image.index.v1+json",
        "application/vnd.docker.distribution.manifest.list.v2+json",
        "application/vnd.oci.image.manifest.v1+json",
        "application/vnd.docker.distribution.manifest.v2+json",
    };

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<NexusRegistryOptions> _options;
    private readonly ILogger<NexusContainerRegistryClient> _logger;

    public NexusContainerRegistryClient(
        HttpClient http,
        IOptionsMonitor<NexusRegistryOptions> options,
        ILogger<NexusContainerRegistryClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<ContainerImageResolution?> ResolveAsync(
        string registry, string repository, string name, string tag, CancellationToken cancellationToken = default)
    {
        var baseUrl = BaseUrlOrNull();
        if (baseUrl is null) return null;

        var imagePath = $"{repository}/{name}";
        var uri = new Uri($"{baseUrl}/v2/{imagePath}/manifests/{Uri.EscapeDataString(tag)}");

        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        foreach (var media in ManifestAccept) req.Headers.Accept.ParseAdd(media);
        ApplyAuth(req);

        try
        {
            using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();

            var digest = resp.Headers.TryGetValues("Docker-Content-Digest", out var values)
                ? values.FirstOrDefault()
                : null;
            if (string.IsNullOrWhiteSpace(digest)) return null;

            var digestRef = $"{registry}/{imagePath}@{digest}";
            return new ContainerImageResolution(digest, digestRef);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "[nexus] Failed to resolve {Image}:{Tag}", imagePath, tag);
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> ListTagsAsync(
        string registry, string repository, string name, CancellationToken cancellationToken = default)
    {
        var baseUrl = BaseUrlOrNull();
        if (baseUrl is null) return Array.Empty<string>();

        var imagePath = $"{repository}/{name}";
        var uri = new Uri($"{baseUrl}/v2/{imagePath}/tags/list");

        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        ApplyAuth(req);

        try
        {
            using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotFound) return Array.Empty<string>();
            resp.EnsureSuccessStatusCode();

            var doc = await resp.Content.ReadFromJsonAsync<TagsListResponse>(cancellationToken).ConfigureAwait(false);
            return doc?.Tags ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "[nexus] Failed to list tags for {Image}", imagePath);
            return Array.Empty<string>();
        }
    }

    private string? BaseUrlOrNull()
    {
        var url = _options.CurrentValue.ApiBaseUrl;
        return string.IsNullOrWhiteSpace(url) ? null : url.TrimEnd('/');
    }

    private void ApplyAuth(HttpRequestMessage req)
    {
        var opts = _options.CurrentValue;
        if (string.IsNullOrEmpty(opts.Username)) return;
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opts.Username}:{opts.Password}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private sealed record TagsListResponse(string? Name, List<string>? Tags);
}
