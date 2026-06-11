using Deployment.Application.Features.Catalog.ContainerImages;
using Deployment.Application.Features.Releases;
using Deployment.Contracts.Releases;
using Deployment.Domain.Abstractions;
using Deployment.Domain.ContainerImages;
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
    private readonly IContainerImageRepository _images;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public PublishReleaseHandler(
        IReleaseRepository releases,
        IContainerImageRepository images,
        IUnitOfWork uow,
        TimeProvider clock)
    {
        _releases = releases;
        _images = images;
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

        var now = _clock.GetUtcNow();
        var release = new Release(
            id: cmd.Id,
            deployableUnitId: cmd.DeployableUnitId,
            semanticVersion: cmd.SemanticVersion,
            buildNumber: cmd.BuildNumber,
            commitSha: cmd.CommitSha,
            artifactType: cmd.ArtifactType.ToDomain(),
            artifactUri: cmd.ArtifactUri,
            createdAtUtc: now);

        await _releases.AddAsync(release, cancellationToken).ConfigureAwait(false);

        // Auto-materialize the container coordinate (decision #1): a ContainerImage
        // release whose ArtifactUri parses to registry/repository/name upserts the
        // coordinate in the same transaction so it shows up in the picker. Best-effort —
        // an unparseable ref simply skips (never fails the publish).
        await UpsertContainerImageAsync(cmd, now, cancellationToken).ConfigureAwait(false);

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return release.Id;
    }

    private async Task UpsertContainerImageAsync(PublishReleaseCommand cmd, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (cmd.ArtifactType != ArtifactTypeDto.ContainerImage) return;
        if (!ContainerImageRef.TryParse(cmd.ArtifactUri, out var registry, out var repository, out var name)) return;

        var coordinate = await _images.FindByCoordinateAsync(registry, repository, name, cancellationToken).ConfigureAwait(false);
        if (coordinate is not null) return;

        await _images.AddAsync(
            new ContainerImage(Guid.NewGuid(), registry, repository, name, defaultTag: null, now),
            cancellationToken).ConfigureAwait(false);
    }
}
