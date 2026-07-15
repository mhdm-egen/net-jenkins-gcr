namespace Deployment.Domain.Mappings;

/// <summary>How a Kubernetes deploy rolls out a new version.</summary>
public enum RolloutStrategy
{
    /// <summary>Apply the Deployment in place (rolling update). The current default behavior.</summary>
    Direct = 0,

    /// <summary>Deploy the new version to a parallel "green" slot, health-gate it, then cut traffic
    /// over by swapping the Service selector. Auto-rollback if green never goes healthy.</summary>
    BlueGreen = 1,
}

/// <summary>For <see cref="RolloutStrategy.BlueGreen"/>, when to cut traffic over to the green slot.</summary>
public enum PromotionMode
{
    /// <summary>Promote automatically once green passes the health gate.</summary>
    Automatic = 0,

    /// <summary>Park the run once green is healthy and wait for a human to promote (or roll back).</summary>
    Manual = 1,
}
