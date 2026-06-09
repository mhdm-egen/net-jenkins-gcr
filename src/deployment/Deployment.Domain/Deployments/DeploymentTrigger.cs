namespace Deployment.Domain.Deployments;

public enum DeploymentTrigger
{
    Manual = 0,
    Pipeline = 1,
    AutoPromote = 2,
}
