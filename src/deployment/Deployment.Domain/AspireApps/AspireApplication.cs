using Deployment.Domain.Common;
using Deployment.Domain.AspireApps.Events;

namespace Deployment.Domain.AspireApps;

/// <summary>
/// A registered .NET Aspire application deployed as a whole (its AppHost manifest is the composition).
/// Deploying it shells out to Aspir8 (<c>aspirate generate --skip-build</c> + <c>apply</c>) against a
/// Kubernetes context, pulling the already-pushed images from Nexus. Distinct from the per-service
/// <see cref="Services.Service"/> + <see cref="Mappings.DeploymentMapping"/> (Cloud Run) model.
/// </summary>
public sealed class AspireApplication : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string? Description { get; private set; }

    /// <summary>Server-local path to the Aspire AppHost directory (must contain an <c>aspirate.json</c>).</summary>
    public string AppHostPath { get; private set; }

    /// <summary>Target Kubernetes context (e.g. <c>docker-desktop</c>).</summary>
    public string KubeContext { get; private set; }

    /// <summary>Target Kubernetes namespace.</summary>
    public string Namespace { get; private set; }

    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private AspireApplication()
    {
        Name = string.Empty;
        AppHostPath = string.Empty;
        KubeContext = string.Empty;
        Namespace = string.Empty;
    }

    public AspireApplication(Guid id, string name, string? description, string appHostPath, string kubeContext, string @namespace, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        Id = id;
        Name = Require(name, nameof(name));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        AppHostPath = Require(appHostPath, nameof(appHostPath));
        KubeContext = Require(kubeContext, nameof(kubeContext));
        Namespace = Require(@namespace, nameof(@namespace));
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        RaiseEvent(new AspireApplicationRegistered(Id, Name, createdAtUtc));
    }

    public void Update(string name, string? description, string appHostPath, string kubeContext, string @namespace, DateTimeOffset occurredAtUtc)
    {
        Name = Require(name, nameof(name));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        AppHostPath = Require(appHostPath, nameof(appHostPath));
        KubeContext = Require(kubeContext, nameof(kubeContext));
        Namespace = Require(@namespace, nameof(@namespace));
        UpdatedAtUtc = occurredAtUtc;
        RaiseEvent(new AspireApplicationUpdated(Id, Name, occurredAtUtc));
    }

    public void ChangeActivation(bool active, DateTimeOffset occurredAtUtc)
    {
        if (IsActive == active) return;
        IsActive = active;
        UpdatedAtUtc = occurredAtUtc;
    }

    private static string Require(string value, string name)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} cannot be empty.", name) : value.Trim();
}
