using Jenkins.Application.Abstractions;
using Jenkins.Contracts.Handoffs;
using Jenkins.Domain.Abstractions;
using Jenkins.Domain.Builds;
using Jenkins.Domain.Handoffs;
using Jenkins.Domain.SourceRepositories;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Jenkins.Application.Features.Handoffs;

/// <summary>
/// Promote a green container build to a deployment Release — the CI→deployment seam
/// (handoff §7). Persists a Pending handoff, calls the deployment Releases API
/// (publish + provenance), then settles the handoff Published/Failed. Idempotent:
/// a duplicate-version conflict resolves the existing release id (decision #4), and
/// an already-published artifact short-circuits.
/// </summary>
public sealed record PromoteToReleaseCommand(
    Guid BuildId,
    Guid BuildArtifactId,
    string RequestedByPrincipal);

public sealed class PromoteToReleaseValidator : AbstractValidator<PromoteToReleaseCommand>
{
    public PromoteToReleaseValidator()
    {
        RuleFor(x => x.BuildId).NotEmpty();
        RuleFor(x => x.BuildArtifactId).NotEmpty();
        RuleFor(x => x.RequestedByPrincipal).NotEmpty().MaximumLength(200);
    }
}

public sealed class PromoteToReleaseHandler
{
    private readonly IBuildStore _builds;
    private readonly ISourceRepositoryStore _repositories;
    private readonly IContainerReleaseHandoffStore _handoffs;
    private readonly IDeploymentReleaseClient _deployment;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    private readonly ILogger<PromoteToReleaseHandler> _logger;

    public PromoteToReleaseHandler(
        IBuildStore builds,
        ISourceRepositoryStore repositories,
        IContainerReleaseHandoffStore handoffs,
        IDeploymentReleaseClient deployment,
        IUnitOfWork uow,
        TimeProvider clock,
        ILogger<PromoteToReleaseHandler> logger)
    {
        _builds = builds;
        _repositories = repositories;
        _handoffs = handoffs;
        _deployment = deployment;
        _uow = uow;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ContainerReleaseHandoffDto> HandleAsync(PromoteToReleaseCommand cmd, CancellationToken cancellationToken = default)
    {
        // Already published? Short-circuit (idempotent re-promote).
        var prior = await _handoffs.FindLatestByArtifactAsync(cmd.BuildArtifactId, cancellationToken).ConfigureAwait(false);
        if (prior is { Status: HandoffStatus.Published })
            return prior.ToDto();

        var build = await _builds.GetByIdAsync(cmd.BuildId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Build {cmd.BuildId} not found.");
        if (build.Status != BuildStatus.Succeeded)
            throw new InvalidOperationException($"Build {build.Id} is {build.Status}; only Succeeded builds can be promoted.");

        var artifact = build.Artifacts.FirstOrDefault(a => a.Id == cmd.BuildArtifactId)
            ?? throw new InvalidOperationException($"Artifact {cmd.BuildArtifactId} not found on build {build.Id}.");
        if (!artifact.IsContainerImage)
            throw new InvalidOperationException($"Artifact '{artifact.Name}' is not a container image.");

        var publication = artifact.NexusPublication()
            ?? throw new InvalidOperationException($"Artifact '{artifact.Name}' has no successful Nexus publication to hand off.");
        if (build.Versions is null)
            throw new InvalidOperationException($"Build {build.Id} has no resolved versions.");
        if (build.Quality is null)
            throw new InvalidOperationException($"Build {build.Id} has no SBOM/vulnerability provenance.");

        var repo = await _repositories.GetByIdAsync(build.RepositoryId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Repository {build.RepositoryId} not found.");
        var component = repo.MatchComponent(artifact.Name)
            ?? throw new InvalidOperationException(
                $"No active deployable component maps container '{artifact.Name}' to a deployment service.");

        var semanticVersion = build.Versions.PackageVersion;
        var artifactUri = publication.Reference;

        // Persist the Pending intent before the external call so the handoff is a
        // durable record even if the deployment service is unreachable.
        var handoff = new ContainerReleaseHandoff(
            id: Guid.NewGuid(),
            buildId: build.Id,
            buildArtifactId: artifact.Id,
            deployableComponentId: component.Id,
            repositoryId: repo.Id,
            deployableUnitId: component.DeployableUnitId,
            semanticVersion: semanticVersion,
            artifactUri: artifactUri,
            requestedByPrincipal: cmd.RequestedByPrincipal,
            createdAtUtc: _clock.GetUtcNow());
        await _handoffs.AddAsync(handoff, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var releaseId = await _deployment.PublishContainerReleaseAsync(
                new PublishReleaseInput(
                    DeployableUnitId: component.DeployableUnitId,
                    SemanticVersion: semanticVersion,
                    BuildNumber: $"#{build.CiBuildNumber}",
                    CommitSha: build.SourceRevision.CommitSha,
                    ArtifactUri: artifactUri),
                cancellationToken).ConfigureAwait(false);

            await _deployment.AttachProvenanceAsync(
                releaseId,
                new AttachProvenanceInput(
                    ArtifactSha256: artifact.Digest,
                    SbomUri: build.Quality.SbomUri,
                    VulnerabilityReportUri: build.Quality.VulnerabilityReportUri,
                    CiRunUrl: build.CiRunUrl,
                    CiRunId: build.CiRunId,
                    PublishedByPrincipal: cmd.RequestedByPrincipal),
                cancellationToken).ConfigureAwait(false);

            handoff.MarkPublished(releaseId, _clock.GetUtcNow());
        }
        catch (DeploymentReleaseConflictException)
        {
            // Version already exists — resolve the existing id and settle idempotently.
            var existing = await _deployment
                .GetReleaseIdByVersionAsync(component.DeployableUnitId, semanticVersion, cancellationToken)
                .ConfigureAwait(false);
            if (existing is Guid existingId)
                handoff.MarkPublished(existingId, _clock.GetUtcNow());
            else
                handoff.MarkFailed($"Release {semanticVersion} already exists but could not be resolved.", _clock.GetUtcNow());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[handoff] Promotion of build {Build} artifact {Artifact} failed.", build.Id, artifact.Id);
            handoff.MarkFailed(ex.Message, _clock.GetUtcNow());
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return handoff.ToDto();
    }
}
