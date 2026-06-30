namespace Deployment.Infrastructure.Nexus;

/// <summary>
/// How the deployment service reaches the Nexus docker registry's v2 API to resolve image digests
/// (for digest-pinning Aspire deploys). Bound from <c>Deployment:Nexus</c>. The URL is whatever the
/// SERVICE can reach (e.g. <c>http://localhost:8082</c>); the resolved digest is host-independent, so
/// it pins correctly regardless of the registry host the cluster pulls from.
/// </summary>
public sealed class NexusRegistryOptions
{
    public const string SectionName = "Deployment:Nexus";

    public string RegistryV2Url { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool Enabled => !string.IsNullOrWhiteSpace(RegistryV2Url);
}
