using Deployment.Application.Features.Environments;
using Deployment.Contracts.Environments;
using Deployment.Domain.Abstractions;
using Deployment.Domain.Environments;
using FluentValidation;
using Environment = Deployment.Domain.Environments.Environment;

namespace Deployment.Application.Features.Environments.RegisterEnvironment;

public sealed record RegisterEnvironmentCommand(
    Guid Id,
    string Name,
    int PromotionRank,
    bool RequiresApproval,
    bool IsProduction);

public sealed class RegisterEnvironmentValidator : AbstractValidator<RegisterEnvironmentCommand>
{
    public RegisterEnvironmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PromotionRank).GreaterThanOrEqualTo(0);
    }
}

public sealed class RegisterEnvironmentHandler
{
    private readonly IEnvironmentRepository _environments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public RegisterEnvironmentHandler(IEnvironmentRepository environments, IUnitOfWork uow, TimeProvider clock)
    {
        _environments = environments;
        _uow = uow;
        _clock = clock;
    }

    public async Task<EnvironmentDto> HandleAsync(RegisterEnvironmentCommand cmd, CancellationToken cancellationToken = default)
    {
        var existing = await _environments.FindByNameAsync(cmd.Name, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            throw new InvalidOperationException($"An environment named '{cmd.Name}' already exists.");

        var env = new Environment(cmd.Id, cmd.Name, cmd.PromotionRank,
            cmd.RequiresApproval, cmd.IsProduction, _clock.GetUtcNow());

        await _environments.AddAsync(env, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return env.ToDto();
    }
}
