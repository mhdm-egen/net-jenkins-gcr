namespace Deployment.Domain.Deployments;

/// <summary>
/// Per-deployment snapshot of a resolved versioned Key Vault URI (decisions §4.3).
/// Written when the <c>Deployment</c> row is created; immutable thereafter so it
/// survives downstream secret rotation. Lets incident response answer
/// "what exact secret version was this deployment using at time T?"
///
/// Composite identity: (DeploymentId, ConfigurationSettingId). Child of the
/// Deployment aggregate; no standalone <see cref="Id"/>.
/// </summary>
public sealed class DeploymentSecretBinding
{
    public Guid DeploymentId { get; private set; }
    public Guid ConfigurationSettingId { get; private set; }

    /// <summary>Versioned Key Vault URI, e.g. <c>https://vault.vault.azure.net/secrets/foo/abc123</c>.</summary>
    public string ResolvedSecretUri { get; private set; }
    public DateTimeOffset ResolvedAtUtc { get; private set; }

    private DeploymentSecretBinding()
    {
        ResolvedSecretUri = string.Empty;
    }

    internal DeploymentSecretBinding(
        Guid deploymentId,
        Guid configurationSettingId,
        string resolvedSecretUri,
        DateTimeOffset resolvedAtUtc)
    {
        if (deploymentId == Guid.Empty)
            throw new ArgumentException("DeploymentId cannot be empty.", nameof(deploymentId));
        if (configurationSettingId == Guid.Empty)
            throw new ArgumentException("ConfigurationSettingId cannot be empty.", nameof(configurationSettingId));
        if (string.IsNullOrWhiteSpace(resolvedSecretUri))
            throw new ArgumentException("ResolvedSecretUri cannot be empty.", nameof(resolvedSecretUri));

        DeploymentId = deploymentId;
        ConfigurationSettingId = configurationSettingId;
        ResolvedSecretUri = resolvedSecretUri.Trim();
        ResolvedAtUtc = resolvedAtUtc;
    }
}
