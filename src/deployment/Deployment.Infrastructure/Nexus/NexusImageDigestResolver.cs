using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Deployment.Infrastructure.Nexus;

/// <summary>Resolves the immutable <c>sha256</c> digest of a repository:tag from the Nexus docker v2 API.</summary>
public interface INexusImageDigestResolver
{
    /// <summary>Returns the <c>sha256:…</c> digest, or null if Nexus isn't configured / the lookup fails.</summary>
    Task<string?> ResolveDigestAsync(string repository, string tag, CancellationToken cancellationToken = default);
}

internal sealed class NexusImageDigestResolver : INexusImageDigestResolver
{
    // Singleton service → one shared HttpClient is the recommended pattern.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly IOptionsMonitor<NexusRegistryOptions> _options;
    private readonly ILogger<NexusImageDigestResolver> _logger;

    public NexusImageDigestResolver(IOptionsMonitor<NexusRegistryOptions> options, ILogger<NexusImageDigestResolver> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<string?> ResolveDigestAsync(string repository, string tag, CancellationToken cancellationToken = default)
    {
        var o = _options.CurrentValue;
        if (!o.Enabled) return null;

        try
        {
            var url = $"{o.RegistryV2Url.TrimEnd('/')}/v2/{repository}/manifests/{Uri.EscapeDataString(tag)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            // A HEAD would suffice but some registries only return the digest header on GET.
            req.Headers.Accept.ParseAdd("application/vnd.docker.distribution.manifest.v2+json");
            req.Headers.Accept.ParseAdd("application/vnd.oci.image.index.v1+json");
            req.Headers.Accept.ParseAdd("application/vnd.oci.image.manifest.v1+json");
            if (!string.IsNullOrEmpty(o.Username))
            {
                var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{o.Username}:{o.Password}"));
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            }

            using var resp = await Http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogDebug("[aspire] digest lookup {Repo}:{Tag} -> HTTP {Code}", repository, tag, (int)resp.StatusCode);
                return null;
            }
            if (resp.Headers.TryGetValues("Docker-Content-Digest", out var values))
            {
                var digest = values.FirstOrDefault();
                return string.IsNullOrWhiteSpace(digest) ? null : digest.Trim();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[aspire] digest lookup {Repo}:{Tag} failed.", repository, tag);
            return null;
        }
    }
}
