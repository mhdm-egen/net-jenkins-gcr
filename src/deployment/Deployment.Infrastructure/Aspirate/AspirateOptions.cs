namespace Deployment.Infrastructure.Aspirate;

/// <summary>
/// Options for the Aspir8 (<c>aspirate</c>) shell-out. Bound from <c>Deployment:Aspirate</c>. The CLI
/// must be installed (<c>dotnet tool install -g Aspirate</c>) and on PATH, or point
/// <see cref="Executable"/> at its full path. Cluster auth is ambient (the kube context's kubeconfig).
/// </summary>
public sealed class AspireOptions
{
    public const string SectionName = "Deployment:Aspirate";

    /// <summary>The aspirate executable — a bare name resolved on PATH, or a full path.</summary>
    public string Executable { get; set; } = "aspirate";

    /// <summary>Optional kubeconfig path passed to aspirate (KUBECONFIG); empty = the process default (~/.kube/config).</summary>
    public string Kubeconfig { get; set; } = string.Empty;

    /// <summary>Root dir for per-run working directories (fetched/extracted manifests); empty = system temp.</summary>
    public string WorkingRoot { get; set; } = string.Empty;

    /// <summary>Image pull policy written into the generated manifests.</summary>
    public string ImagePullPolicy { get; set; } = "IfNotPresent";

    /// <summary>
    /// Registry host the CLUSTER pulls from (e.g. <c>host.docker.internal:8082</c> for a local cluster
    /// reaching the host's Nexus). When set, the runner rewrites the build registry host baked into the
    /// generated manifests to this, via a Kustomize <c>images:</c> override — so the node can resolve the
    /// images even when the build/push host (e.g. <c>localhost:8082</c>) is not node-reachable. Empty =
    /// deploy the manifests as generated.
    /// </summary>
    public string PullRegistry { get; set; } = string.Empty;

    /// <summary>
    /// When true, before <c>aspirate apply</c> the runner provisions a <c>kubernetes.io/dockerconfigjson</c>
    /// image-pull secret (named <see cref="PullSecretName"/>) in the target namespace from the
    /// <c>Deployment:Nexus</c> credentials for the <see cref="PullRegistry"/> host, and adds it to the
    /// namespace's <c>default</c> ServiceAccount — so aspirate-deployed pods can pull the (auth-required)
    /// Nexus images. Requires <see cref="PullRegistry"/> + Nexus Username/Password. Off by default.
    /// (Insecure/HTTP registries still need the node's container runtime configured; this only handles auth.)
    /// </summary>
    public bool EnsurePullSecret { get; set; }

    /// <summary>Name of the image-pull secret provisioned when <see cref="EnsurePullSecret"/> is set.</summary>
    public string PullSecretName { get; set; } = "nexus-pull";

    /// <summary>
    /// Base URL (scheme + authority, e.g. <c>http://localhost:8081</c>) this service should fetch
    /// manifest archives from, replacing whatever host was recorded on the application.
    ///
    /// The recorded <c>ManifestSource</c> comes from CI, which uploads from a build agent and records
    /// the address IT used — <c>http://nexus:8081/...</c> by default. This service runs as a HOST
    /// PROCESS under the Aspire AppHost, so it cannot resolve that container hostname, and a
    /// CI-triggered auto-deploy would fail at the fetch. Note host.docker.internal does not help
    /// here either: it resolves inside containers but not from a Windows host.
    ///
    /// Only the scheme + authority are replaced; the path (repository, app, version, filename) is
    /// preserved, so this cannot repoint a deploy at a different artifact. Empty = use the recorded
    /// URL unchanged, which is correct when CI and this service share a view of the registry.
    /// This is the manifest-fetch counterpart of <see cref="PullRegistry"/>.
    /// </summary>
    public string ManifestBaseUrl { get; set; } = string.Empty;

    /// <summary>How long <c>aspirate generate</c> may run (builds the Aspire manifest).</summary>
    public int GenerateTimeoutSeconds { get; set; } = 600;

    /// <summary>How long <c>aspirate apply</c> may run.</summary>
    public int ApplyTimeoutSeconds { get; set; } = 300;
}
