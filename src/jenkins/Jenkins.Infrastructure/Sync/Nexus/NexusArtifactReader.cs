using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Jenkins.Application.Features.Builds;
using Jenkins.Contracts.Builds;
using Microsoft.Extensions.Logging;

namespace Jenkins.Infrastructure.Sync.Nexus;

/// <summary>
/// Minimal Nexus REST client for artifact reconciliation (option b). Uses the
/// Search API (<c>service/rest/v1/search</c>) to find the build's published
/// components by version/tag, and reads the digest from each component's asset
/// checksums. Read-only; a focused subset of the web UI's NexusClient.
/// </summary>
internal sealed class NexusArtifactReader : INexusArtifactReader, IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly NexusReconcileOptions _options;
    private readonly ILogger<NexusArtifactReader> _logger;

    public NexusArtifactReader(NexusReconcileOptions options, ILogger<NexusArtifactReader> logger)
    {
        _options = options;
        _logger = logger;
        _http = new HttpClient { BaseAddress = new Uri(_options.Url.TrimEnd('/') + "/") };
        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.User}:{_options.Password}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<IReadOnlyList<ArtifactSpec>> FindArtifactsAsync(
        string packageVersion, string commitShort, int ciBuildNumber, CancellationToken cancellationToken = default)
    {
        var specs = new List<ArtifactSpec>();

        // --- NuGet: components whose version == PackageVersion ---
        foreach (var c in await SearchAsync(_options.NuGetRepository, packageVersion, cancellationToken).ConfigureAwait(false))
        {
            if (!string.Equals(c.Version, packageVersion, StringComparison.OrdinalIgnoreCase)) continue;
            var nupkg = c.Assets?.FirstOrDefault(a => a.Path?.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) == true)
                        ?? c.Assets?.FirstOrDefault();
            var sha = nupkg?.Checksum?.Sha256;
            if (string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(sha)) continue;

            specs.Add(new ArtifactSpec(
                Kind: ArtifactKindDto.NuGetPackage,
                Name: c.Name!,
                Version: packageVersion,
                Digest: sha!,
                SizeBytes: nupkg?.FileSize,
                Registry: PublicationRegistryDto.NexusNuGet,
                Reference: nupkg?.DownloadUrl ?? string.Empty,
                Tags: new[] { packageVersion }));
        }

        // --- Docker: the image tagged with the commit short (fallback ci-N) ---
        if (!string.IsNullOrWhiteSpace(_options.DockerRegistryHost))
        {
            var tag = commitShort;
            var dockerComponents = await SearchAsync(_options.DockerRepository, tag, cancellationToken).ConfigureAwait(false);
            if (dockerComponents.Count == 0)
            {
                tag = $"ci-{ciBuildNumber}";
                dockerComponents = await SearchAsync(_options.DockerRepository, tag, cancellationToken).ConfigureAwait(false);
            }

            var host = _options.DockerRegistryHost.TrimEnd('/');
            foreach (var c in dockerComponents)
            {
                if (!string.Equals(c.Version, tag, StringComparison.OrdinalIgnoreCase)) continue;
                var manifest = c.Assets?.FirstOrDefault(a => a.ContentType?.Contains("manifest", StringComparison.OrdinalIgnoreCase) == true)
                               ?? c.Assets?.FirstOrDefault();
                var sha = manifest?.Checksum?.Sha256;
                if (string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(sha)) continue;

                var digest = $"sha256:{sha}";
                specs.Add(new ArtifactSpec(
                    Kind: ArtifactKindDto.ContainerImage,
                    Name: c.Name!,
                    Version: packageVersion,
                    Digest: digest,
                    SizeBytes: manifest?.FileSize,
                    Registry: PublicationRegistryDto.NexusDocker,
                    Reference: $"{host}/{c.Name}@{digest}",
                    Tags: new[] { c.Version ?? tag }));
            }
        }
        else
        {
            _logger.LogDebug("Nexus DockerRegistryHost not set — skipping container artifact reconciliation.");
        }

        return specs;
    }

    private async Task<List<ComponentDto>> SearchAsync(string repository, string version, CancellationToken ct)
    {
        var results = new List<ComponentDto>();
        string? token = null;
        do
        {
            var path = $"service/rest/v1/search?repository={Uri.EscapeDataString(repository)}&version={Uri.EscapeDataString(version)}";
            if (token is { Length: > 0 })
                path += $"&continuationToken={Uri.EscapeDataString(token)}";

            var page = await _http.GetFromJsonAsync<SearchListDto>(path, Json, ct).ConfigureAwait(false);
            if (page?.Items is { Length: > 0 })
                results.AddRange(page.Items);
            token = page?.ContinuationToken;
        }
        while (!string.IsNullOrEmpty(token));

        return results;
    }

    public void Dispose() => _http.Dispose();

    // --- Nexus REST v1 DTOs (subset) ---
    private sealed record SearchListDto(ComponentDto[]? Items, string? ContinuationToken);
    private sealed record ComponentDto(string? Id, string? Name, string? Version, string? Format, AssetDto[]? Assets);
    private sealed record AssetDto(string? DownloadUrl, string? Path, string? ContentType, ChecksumDto? Checksum, long? FileSize);
    private sealed record ChecksumDto(string? Sha1, string? Sha256);
}
