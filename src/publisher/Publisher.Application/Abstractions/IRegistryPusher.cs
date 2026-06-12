using Publisher.Domain.Registries;

namespace Publisher.Application.Abstractions;

/// <summary>
/// Resolved credential handed to the pusher at run time. The secret value is materialized just
/// before the push (from a secret store) and never persisted. Null secret means ambient auth
/// (e.g. ADC / Workload Identity).
/// </summary>
public sealed record RegistryCredential(RegistryAuthMethod Method, string? Username, string? Secret);

/// <summary>
/// Performs a registry-to-registry image copy (local Nexus → remote). Implementations should be
/// digest-preserving and idempotent. The infrastructure default shells out to <c>crane copy</c>.
/// </summary>
public interface IRegistryPusher
{
    Task PushAsync(string sourceRef, string destinationRef, RegistryCredential credential, CancellationToken cancellationToken = default);
}
