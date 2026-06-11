namespace Deployment.Infrastructure.Registry;

/// <summary>
/// Options for <see cref="NexusContainerRegistryClient"/>. Bound from configuration
/// section <c>"Deployment:NexusRegistry"</c>.
///
/// The password is read from configuration (set it via the
/// <c>Deployment__NexusRegistry__Password</c> environment variable — never commit it).
/// When <see cref="ApiBaseUrl"/> is empty the resolver degrades gracefully: tag listing
/// returns empty and resolution returns null, so the rest of the system still works
/// without a configured registry.
/// </summary>
public sealed class NexusRegistryOptions
{
    public const string SectionName = "Deployment:NexusRegistry";

    /// <summary>Root URL of the Docker Registry v2 API, e.g. <c>http://nexus:8082</c>.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Basic-auth username (optional; omit for anonymous registries).</summary>
    public string? Username { get; set; }

    /// <summary>Basic-auth password — supply via env var, not appsettings.</summary>
    public string? Password { get; set; }

    /// <summary>Per-request timeout for registry queries.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
