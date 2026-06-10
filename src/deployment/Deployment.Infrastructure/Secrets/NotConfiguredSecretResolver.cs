using Deployment.Application.Abstractions;

namespace Deployment.Infrastructure.Secrets;

/// <summary>
/// Placeholder <see cref="ISecretResolver"/>. v1 hosts that don't yet integrate
/// with a real secret store register this — it throws when invoked so the
/// failure surfaces immediately at deploy-time. Real adapters (Azure Key Vault,
/// HashiCorp Vault, AWS Secrets Manager) land as separate <c>ISecretResolver</c>
/// implementations and replace this registration.
/// </summary>
internal sealed class NotConfiguredSecretResolver : ISecretResolver
{
    public Task<string> ResolveCurrentVersionAsync(
        string secretReference,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            $"No ISecretResolver is configured, but a deployment attempted to resolve secret '{secretReference}'. " +
            "Register a real ISecretResolver implementation in DI before deploying any unit with secret configuration entries.");
}
