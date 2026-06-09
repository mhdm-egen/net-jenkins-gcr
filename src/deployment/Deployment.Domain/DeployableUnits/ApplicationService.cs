namespace Deployment.Domain.DeployableUnits;

/// <summary>
/// Catalog-membership entry inside an <see cref="Application"/> aggregate.
/// Identifies "Service S is part of Application A, in role R, with deployment
/// order O." This is the version-agnostic membership table — distinct from
/// <c>ReleaseComposition</c>, which is the per-release BOM.
///
/// Composite identity: (ApplicationId, ServiceId). No standalone Id — this is
/// a child entity that lives only inside its parent Application.
/// </summary>
public sealed class ApplicationService
{
    public Guid ApplicationId { get; private set; }
    public Guid ServiceId { get; private set; }

    /// <summary>Free-form label for the role the service plays in this application — e.g. "api", "worker", "scheduler".</summary>
    public string Role { get; private set; }

    /// <summary>
    /// True when an Application release MAY omit this service from its BOM.
    /// Optional members don't fail the cascade when missing from a
    /// <c>ReleaseComposition</c>. Defaults to false (required).
    /// </summary>
    public bool IsOptional { get; private set; }

    /// <summary>
    /// Catalog-level cascade order. Lower runs first; ties run in parallel.
    /// See deployment-model-decisions §5.1.
    /// </summary>
    public int DeploymentOrder { get; private set; }

    private ApplicationService()
    {
        Role = string.Empty;
    }

    internal ApplicationService(
        Guid applicationId,
        Guid serviceId,
        string role,
        bool isOptional,
        int deploymentOrder)
    {
        if (applicationId == Guid.Empty) throw new ArgumentException("ApplicationId cannot be empty.", nameof(applicationId));
        if (serviceId == Guid.Empty) throw new ArgumentException("ServiceId cannot be empty.", nameof(serviceId));
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be empty.", nameof(role));

        ApplicationId = applicationId;
        ServiceId = serviceId;
        Role = role.Trim();
        IsOptional = isOptional;
        DeploymentOrder = deploymentOrder;
    }

    internal void Update(string role, bool isOptional, int deploymentOrder)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be empty.", nameof(role));

        Role = role.Trim();
        IsOptional = isOptional;
        DeploymentOrder = deploymentOrder;
    }
}
