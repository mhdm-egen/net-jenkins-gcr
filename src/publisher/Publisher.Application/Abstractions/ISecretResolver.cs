namespace Publisher.Application.Abstractions;

/// <summary>
/// Resolves a secret <i>reference</i> (a name/key, e.g. an env-var key or secret-manager path) to
/// its actual value at run time. Secrets are never stored in the publisher DB — only the reference
/// is. The default implementation reads environment variables; swap for GCP Secret Manager / Key
/// Vault / Vault later without touching the domain.
/// </summary>
public interface ISecretResolver
{
    /// <summary>Resolves the secret, or throws if the reference cannot be found.</summary>
    Task<string> ResolveAsync(string secretRef, CancellationToken cancellationToken = default);
}
