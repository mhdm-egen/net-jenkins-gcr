using Deployment.Contracts.Deployments;
using FluentValidation;

namespace Deployment.Application.Features.Deployments.ApproveDeployment;

public sealed class ApproveDeploymentValidator : AbstractValidator<ApproveDeploymentCommand>
{
    public ApproveDeploymentValidator()
    {
        RuleFor(x => x.DeploymentId).NotEmpty();
        RuleFor(x => x.ApprovalId).NotEmpty();
        RuleFor(x => x.ApproverPrincipal).NotEmpty();
        RuleFor(x => x.Verdict)
            .Must(v => v is ApprovalStatusDto.Approved or ApprovalStatusDto.Rejected)
            .WithMessage("Verdict must be Approved or Rejected.");
    }
}
