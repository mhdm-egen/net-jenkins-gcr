using Deployment.Contracts.Deployments;
using Deployment.Domain.Deployments;

namespace Deployment.Application.Features.Deployments;

internal static class DeploymentMapping
{
    public static DeploymentStrategy ToDomain(this DeploymentStrategyDto s) => (DeploymentStrategy)(int)s;
    public static DeploymentTrigger ToDomain(this DeploymentTriggerDto t) => (DeploymentTrigger)(int)t;
    public static ApprovalStatus ToDomain(this ApprovalStatusDto v) => (ApprovalStatus)(int)v;

    public static DeploymentStatusDto ToDto(this DeploymentStatus s) => (DeploymentStatusDto)(int)s;
    public static DeploymentStrategyDto ToDto(this DeploymentStrategy s) => (DeploymentStrategyDto)(int)s;
    public static DeploymentTriggerDto ToDto(this DeploymentTrigger t) => (DeploymentTriggerDto)(int)t;
    public static ApprovalStatusDto ToDto(this ApprovalStatus v) => (ApprovalStatusDto)(int)v;
}
