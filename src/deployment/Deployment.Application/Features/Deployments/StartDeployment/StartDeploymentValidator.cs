using FluentValidation;

namespace Deployment.Application.Features.Deployments.StartDeployment;

public sealed class StartDeploymentValidator : AbstractValidator<StartDeploymentCommand>
{
    public StartDeploymentValidator()
    {
        RuleFor(x => x.ReleaseId).NotEmpty();
        RuleFor(x => x.EnvironmentId).NotEmpty();
        RuleFor(x => x.TargetIds).NotNull();
        RuleFor(x => x.TriggeredByPrincipal).NotEmpty();
    }
}
