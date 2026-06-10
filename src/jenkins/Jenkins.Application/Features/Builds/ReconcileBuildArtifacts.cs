using Jenkins.Contracts.Builds;
using Jenkins.Domain.Abstractions;
using Jenkins.Domain.Builds;

namespace Jenkins.Application.Features.Builds;

/// <summary>
/// One artifact (+ its registry publication) discovered for a build — the shape the
/// Nexus reconciliation produces.
/// </summary>
public sealed record ArtifactSpec(
    ArtifactKindDto Kind,
    string Name,
    string Version,
    string Digest,
    long? SizeBytes,
    PublicationRegistryDto Registry,
    string Reference,
    IReadOnlyList<string>? Tags);

/// <summary>
/// Idempotently attach artifacts found in Nexus to a build. Skips any artifact the
/// build already has (matched by kind + name + version), so the build-sync can call
/// this every tick until Nexus has the published artifacts (decision: option b).
/// </summary>
public sealed record ReconcileBuildArtifactsCommand(Guid BuildId, IReadOnlyList<ArtifactSpec> Artifacts);

public sealed record ReconcileResult(int TotalArtifacts, int Added);

public sealed class ReconcileBuildArtifactsHandler
{
    private readonly IBuildStore _builds;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public ReconcileBuildArtifactsHandler(IBuildStore builds, IUnitOfWork uow, TimeProvider clock)
    {
        _builds = builds;
        _uow = uow;
        _clock = clock;
    }

    public async Task<ReconcileResult> HandleAsync(ReconcileBuildArtifactsCommand cmd, CancellationToken cancellationToken = default)
    {
        var build = await _builds.GetByIdAsync(cmd.BuildId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Build {cmd.BuildId} not found.");

        var now = _clock.GetUtcNow();
        var added = 0;

        foreach (var spec in cmd.Artifacts)
        {
            var kind = (ArtifactKind)(int)spec.Kind;
            var exists = build.Artifacts.Any(a =>
                a.Kind == kind
                && string.Equals(a.Name, spec.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.Version, spec.Version, StringComparison.Ordinal));
            if (exists) continue;

            var artifact = build.AddArtifact(Guid.NewGuid(), kind, spec.Name, spec.Version, spec.Digest, spec.SizeBytes, now);
            if (!string.IsNullOrWhiteSpace(spec.Reference))
            {
                build.AddPublication(
                    artifactId: artifact.Id,
                    publicationId: Guid.NewGuid(),
                    registry: (PublicationRegistry)(int)spec.Registry,
                    reference: spec.Reference,
                    tags: spec.Tags,
                    status: PublicationStatus.Pushed,
                    publishedAtUtc: now);
            }
            added++;
        }

        if (added > 0)
            await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ReconcileResult(build.Artifacts.Count, added);
    }
}
