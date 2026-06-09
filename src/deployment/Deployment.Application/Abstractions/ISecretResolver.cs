namespace Deployment.Application.Abstractions;

/// <summary>
/// Resolves a secret reference (e.g., <c>https://vault.vault.azure.net/secrets/foo</c>
/// or <c>vault://foo</c>) to its **current versioned URI**. The version is
/// snapshotted into a <c>DeploymentSecretBinding</c> at deploy-start time so
/// the deployment carries a fixed secret-version pointer that survives
/// downstream secret rotation (decisions §4.3).
///
/// Implemented by an Infrastructure adapter that talks to Key Vault / SOPS / etc.
/// </summary>
public interface ISecretResolver
{
    Task<string> ResolveCurrentVersionAsync(
        string secretReference,
        CancellationToken cancellationToken = default);
}
