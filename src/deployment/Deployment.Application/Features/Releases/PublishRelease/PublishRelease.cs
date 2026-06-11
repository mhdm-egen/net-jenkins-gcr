using Deployment.Application.Features.Releases;
using Deployment.Contracts.Releases;
using Deployment.Domain.Abstractions;
using Deployment.Domain.Releases;
using FluentValidation;

namespace Deployment.Application.Features.Releases.PublishRelease;

/// <summary>
/// Publish (register) a new <see cref="Release"/> for a deployable unit.
/// CI pipelines call this once a build succeeds; the row goes straight to
/// <see cref="ReleaseStatus.Available"/> and is then deployable.
///
/// Status changes and provenance attachment are separate commands so CI can
/// publish the artifact early and attach SBOM / scan results once they finish.
/// </summary>
public sealed record PublishReleaseCommand(
    Guid Id,
    Guid DeployableUnitId,
    string SemanticVersion,
    string BuildNumber,
    string CommitSha,
    ArtifactTypeDto ArtifactType,
    string? ArtifactUri);

public sealed class PublishReleaseValidator : AbstractValidator<PublishReleaseCommand>
{
    public PublishReleaseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DeployableUnitId).NotEmpty();
        RuleFor(x => x.SemanticVersion).NotEmpty().MaximumLength(64);
        RuleFor(x => x.BuildNumber).NotEmpty().MaximumLength(64);
        RuleFor(x => x.CommitSha).NotEmpty().MaximumLength(64);

        // Manifest = Application release (BOM-only); other types require an artifact URI.
        RuleFor(x => x.ArtifactUri)
            .Empty()
            .When(x => x.ArtifactType == ArtifactTypeDto.Manifest)
            .WithMessage("ArtifactUri must be empty for Manifest (Application) releases.");
        RuleFor(x => x.ArtifactUri)
            .NotEmpty()
            .When(x => x.ArtifactType != ArtifactTypeDto.Manifest)
            .WithMessage("ArtifactUri is required for non-Manifest artifact types.");
    }
}

public sealed class PublishReleaseHandler
{
    private readonly IReleaseRepository _releases;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public PublishReleaseHandler(IReleaseRepository releases, IUnitOfWork uow, TimeProvider clock)
    {
        _releases = releases;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Guid> HandleAsync(PublishReleaseCommand cmd, CancellationToken cancellationToken = default)
    {
        var existing = await _releases.FindByVersionAsync(cmd.DeployableUnitId, cmd.SemanticVersion, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
            throw new InvalidOperationException(
                $"A release of unit {cmd.DeployableUnitId} with version '{cmd.SemanticVersion}' already exists.");

        var release = new Release(
            id: cmd.Id,
            deployableUnitId: cmd.DeployableUnitId,
            semanticVersion: cmd.SemanticVersion,
            buildNumber: cmd.BuildNumber,
            commitSha: cmd.CommitSha,
            artifactType: cmd.ArtifactType.ToDomain(),
            artifactUri: cmd.ArtifactUri,
            createdAtUtc: _clock.GetUtcNow());

        await _releases.AddAsync(release, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return release.Id;
    }
}
