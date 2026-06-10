using Jenkins.Contracts.Builds;
using Jenkins.Domain.Abstractions;
using Jenkins.Domain.Builds;
using FluentValidation;

namespace Jenkins.Application.Features.Builds;

/// <summary>
/// Settle a running build to a terminal state, recording the resolved versions and
/// supply-chain quality outputs observed at completion.
/// </summary>
public sealed record CompleteBuildCommand(
    Guid BuildId,
    BuildStatusDto Status,
    DateTimeOffset CompletedAtUtc,
    long? DurationMs,
    BuildVersionsInput? Versions,
    BuildQualityInput? Quality);

public sealed record BuildVersionsInput(
    string PackageVersion,
    string FileVersion,
    string AssemblyVersion,
    string InformationalVersion,
    string BaseVersion);

public sealed record BuildQualityInput(
    string SbomUri,
    string VulnerabilityReportUri);

public sealed class CompleteBuildValidator : AbstractValidator<CompleteBuildCommand>
{
    public CompleteBuildValidator()
    {
        RuleFor(x => x.BuildId).NotEmpty();
        RuleFor(x => x.Status)
            .Must(s => s is BuildStatusDto.Succeeded or BuildStatusDto.Failed or BuildStatusDto.Aborted)
            .WithMessage("Complete requires a terminal status (Succeeded, Failed, or Aborted).");

        When(x => x.Versions is not null, () =>
        {
            RuleFor(x => x.Versions!.PackageVersion).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Versions!.FileVersion).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Versions!.AssemblyVersion).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Versions!.InformationalVersion).NotEmpty().MaximumLength(300);
            RuleFor(x => x.Versions!.BaseVersion).NotEmpty().MaximumLength(64);
        });

        When(x => x.Quality is not null, () =>
        {
            RuleFor(x => x.Quality!.SbomUri).NotEmpty().MaximumLength(500);
            RuleFor(x => x.Quality!.VulnerabilityReportUri).NotEmpty().MaximumLength(500);
        });
    }
}

public sealed class CompleteBuildHandler
{
    private readonly IBuildStore _builds;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public CompleteBuildHandler(IBuildStore builds, IUnitOfWork uow, TimeProvider clock)
    {
        _builds = builds;
        _uow = uow;
        _clock = clock;
    }

    public async Task<BuildDetailDto> HandleAsync(CompleteBuildCommand cmd, CancellationToken cancellationToken = default)
    {
        var build = await _builds.GetByIdAsync(cmd.BuildId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Build {cmd.BuildId} not found.");

        var now = _clock.GetUtcNow();

        if (cmd.Versions is { } v)
            build.RecordVersions(
                new BuildVersions(v.PackageVersion, v.FileVersion, v.AssemblyVersion, v.InformationalVersion, v.BaseVersion),
                now);

        if (cmd.Quality is { } q)
            build.AttachQuality(new BuildQuality(q.SbomUri, q.VulnerabilityReportUri), now);

        switch (cmd.Status)
        {
            case BuildStatusDto.Succeeded:
                build.MarkSucceeded(cmd.CompletedAtUtc, cmd.DurationMs);
                break;
            case BuildStatusDto.Failed:
                build.MarkFailed(cmd.CompletedAtUtc, cmd.DurationMs);
                break;
            case BuildStatusDto.Aborted:
                build.MarkAborted(cmd.CompletedAtUtc, cmd.DurationMs);
                break;
            default:
                throw new InvalidOperationException("Complete requires a terminal status.");
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return build.ToDetailDto();
    }
}
