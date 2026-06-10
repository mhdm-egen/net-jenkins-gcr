namespace Jenkins.Infrastructure.Sync.Nexus;

/// <summary>
/// Options for the Nexus artifact reconciliation reader. Bound from configuration
/// section <c>"Nexus"</c>. Password supplied via env var <c>Nexus__Password</c> —
/// never committed.
/// </summary>
public sealed class NexusReconcileOptions
{
    public const string SectionName = "Nexus";

    /// <summary>Nexus REST base URL (e.g. <c>http://nexus:8081</c>).</summary>
    public string Url { get; set; } = string.Empty;

    public string User { get; set; } = "admin";
    public string Password { get; set; } = string.Empty;

    public string NuGetRepository { get; set; } = "nuget-hosted";
    public string DockerRepository { get; set; } = "docker-hosted";

    /// <summary>
    /// Docker registry connector host for pull-by-digest references (e.g.
    /// <c>nexus:8082</c>) — the host used in <c>{host}/{name}@sha256:…</c>. When
    /// empty, container artifacts are skipped (no resolvable pull reference).
    /// </summary>
    public string DockerRegistryHost { get; set; } = string.Empty;
}
