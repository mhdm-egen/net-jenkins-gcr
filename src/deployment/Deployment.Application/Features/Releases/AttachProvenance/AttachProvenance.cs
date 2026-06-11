using Deployment.Domain.Abstractions;
using Deployment.Domain.Releases;
using FluentValidation;

namespace Deployment.Application.Features.Releases.AttachProvenance;

/// <summary>
/// Attach the six supply-chain provenance fields to an existing release
/// (decisions §9.1). Re-attaching overwrites — the publish pipeline owns this
/// field. Typically called once a CI SBOM/scan job completes.
/// </summary>
public sealed record AttachProvenanceCommand(
    Guid ReleaseId,
    string ArtifactSha256,
    string SbomUri,
    string VulnerabilityReportUri,
    string CiRunUrl,
    string CiRunId,
    string PublishedByPrincipal);

public sealed class AttachProvenanceValidator : AbstractValidator<AttachProvenanceCommand>
{
    public AttachProvenanceValidator()
    {
        RuleFor(x => x.ReleaseId).NotEmpty();
        RuleFor(x => x.ArtifactSha256).NotEmpty().MaximumLength(128);
        RuleFor(x => x.SbomUri).NotEmpty().MaximumLength(500);
        RuleFor(x => x.VulnerabilityReportUri).NotEmpty().MaximumLength(500);
        RuleFor(x => x.CiRunUrl).NotEmpty().MaximumLength(500);
        RuleFor(x => x.CiRunId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PublishedByPrincipal).NotEmpty().MaximumLength(200);
    }
}

public sealed class AttachProvenanceHandler
{
    private readonly IReleaseRepository _releases;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public AttachProvenanceHandler(IReleaseRepository releases, IUnitOfWork uow, TimeProvider clock)
    {
        _releases = releases;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(AttachProvenanceCommand cmd, CancellationToken cancellationToken = default)
    {
        var release = await _releases.GetByIdAsync(cmd.ReleaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Release {cmd.ReleaseId} not found.");

        var provenance = new Provenance(
            artifactSha256: cmd.ArtifactSha256,
            sbomUri: cmd.SbomUri,
            vulnerabilityReportUri: cmd.VulnerabilityReportUri,
            ciRunUrl: cmd.CiRunUrl,
            ciRunId: cmd.CiRunId,
            publishedByPrincipal: cmd.PublishedByPrincipal);

        release.AttachProvenance(provenance, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
