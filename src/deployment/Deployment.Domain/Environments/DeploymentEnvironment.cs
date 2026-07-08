using Deployment.Domain.Common;
using Deployment.Domain.Environments.Events;

namespace Deployment.Domain.Environments;

/// <summary>
/// A deployment target environment (dev/staging/prod) with its Google Cloud coordinates. The
/// "where": a service is deployed to one or more of these via a mapping.
/// (Named DeploymentEnvironment to avoid clashing with System.Environment.)
/// </summary>
public sealed class DeploymentEnvironment : AggregateRoot<Guid>
{
    public string Name { get; private set; }

    /// <summary>GCP project id, e.g. <c>my-project</c>.</summary>
    public string GcpProject { get; private set; }

    /// <summary>GCP region, e.g. <c>us-central1</c> (Cloud Run location + GAR location).</summary>
    public string Region { get; private set; }

    /// <summary>Google Artifact Registry repository name the container is promoted into.</summary>
    public string GarRepository { get; private set; }

    /// <summary>Kubernetes context (e.g. <c>docker-desktop</c>) — set for environments that target a cluster.</summary>
    public string? KubernetesContext { get; private set; }

    /// <summary>Kubernetes namespace deploys land in — set for cluster-targeting environments.</summary>
    public string? KubernetesNamespace { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>When true, deploys targeting this environment park as AwaitingApproval until a human approves.</summary>
    public bool IsProtected { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private DeploymentEnvironment()
    {
        Name = string.Empty;
        GcpProject = string.Empty;
        Region = string.Empty;
        GarRepository = string.Empty;
    }

    // GCP coordinates are optional now: an environment may instead (or also) target a Kubernetes cluster.
    // The "at least one target" rule lives in the create/update validators, not the aggregate.
    public DeploymentEnvironment(Guid id, string name, string? gcpProject, string? region, string? garRepository,
        string? kubernetesContext, string? kubernetesNamespace, DateTimeOffset createdAtUtc, bool isProtected = false)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be empty.", nameof(name));

        Id = id;
        Name = name.Trim();
        GcpProject = gcpProject?.Trim() ?? string.Empty;
        Region = region?.Trim() ?? string.Empty;
        GarRepository = garRepository?.Trim() ?? string.Empty;
        KubernetesContext = Clean(kubernetesContext);
        KubernetesNamespace = Clean(kubernetesNamespace);
        IsActive = true;
        IsProtected = isProtected;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;

        RaiseEvent(new EnvironmentRegistered(Id, Name, GcpProject, Region, createdAtUtc));
    }

    public void Update(string name, string? gcpProject, string? region, string? garRepository,
        string? kubernetesContext, string? kubernetesNamespace, DateTimeOffset occurredAtUtc, bool isProtected = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be empty.", nameof(name));
        Name = name.Trim();
        GcpProject = gcpProject?.Trim() ?? string.Empty;
        Region = region?.Trim() ?? string.Empty;
        GarRepository = garRepository?.Trim() ?? string.Empty;
        KubernetesContext = Clean(kubernetesContext);
        KubernetesNamespace = Clean(kubernetesNamespace);
        IsProtected = isProtected;
        UpdatedAtUtc = occurredAtUtc;
        RaiseEvent(new EnvironmentUpdated(Id, Name, GcpProject, Region, occurredAtUtc));
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    public void ChangeActivation(bool active, DateTimeOffset occurredAtUtc)
    {
        if (IsActive == active) return;
        IsActive = active;
        UpdatedAtUtc = occurredAtUtc;
    }
}
