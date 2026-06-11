using Deployment.Contracts.Releases;
using FluentValidation;

namespace Deployment.Application.Features.Releases.ChangeReleaseStatus;

public sealed class ChangeReleaseStatusValidator : AbstractValidator<ChangeReleaseStatusCommand>
{
    public ChangeReleaseStatusValidator()
    {
        RuleFor(x => x.ReleaseId).NotEmpty();
        RuleFor(x => x.ChangedByPrincipal).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .When(x => x.NewStatus == ReleaseStatusDto.Quarantined)
            .WithMessage("A reason is required when quarantining a release.");
    }
}
