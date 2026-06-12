using Microsoft.Extensions.Configuration;
using Publisher.Application.Abstractions;

namespace Publisher.Infrastructure.Secrets;

/// <summary>
/// Default <see cref="ISecretResolver"/>: resolves a secret reference from configuration, which
/// includes environment variables (double-underscore keys, e.g.
/// <c>Publisher__Registries__gar-prod__Key</c>). This keeps real credentials out of the DB and
/// out of git. Swap for a GCP Secret Manager / Key Vault resolver later without touching callers.
/// </summary>
internal sealed class EnvironmentSecretResolver : ISecretResolver
{
    private readonly IConfiguration _configuration;

    public EnvironmentSecretResolver(IConfiguration configuration) => _configuration = configuration;

    public Task<string> ResolveAsync(string secretRef, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretRef))
            throw new InvalidOperationException("Secret reference is empty.");

        // Accept both 'A:B:C' and 'A__B__C' forms; configuration treats them equivalently.
        var key = secretRef.Replace("__", ":");
        var value = _configuration[key] ?? Environment.GetEnvironmentVariable(secretRef);

        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException(
                $"Secret '{secretRef}' could not be resolved from configuration or environment.");

        return Task.FromResult(value);
    }
}
