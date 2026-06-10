using Jenkins.Contracts.Builds;
using Jenkins.Domain.Abstractions;
using Jenkins.Domain.Builds;
using FluentValidation;

namespace Jenkins.Application.Features.Builds;

/// <summary>
/// Record an artifact a build produced (a NuGet package or container image) and,
/// optionally, the registry push for it in one call — the shape the Nexus publish
/// stages report.
/// </summary>
public sealed record RecordArtifactCommand(
    Guid BuildId,
    Guid ArtifactId,
    ArtifactKindDto Kind,
    string Name,
    string Version,
    string Digest,
    long? SizeBytes,
    PublicationRegistryDto? Registry,
    string? Reference,
    IReadOnlyList<string>? Tags,
    Guid PublicationId);

public sealed class RecordArtifactValidator : AbstractValidator<RecordArtifactCommand>
{
    public RecordArtifactValidator()
    {
        RuleFor(x => x.BuildId).NotEmpty();
        RuleFor(x => x.ArtifactId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Version).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Digest).NotEmpty().MaximumLength(200);

        When(x => x.Registry is not null, () =>
        {
            RuleFor(x => x.Reference).NotEmpty().MaximumLength(500);
            RuleFor(x => x.PublicationId).NotEmpty();
        });
    }
}

public sealed class RecordArtifactHandler
{
    private readonly IBuildStore _builds;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public RecordArtifactHandler(IBuildStore builds, IUnitOfWork uow, TimeProvider clock)
    {
        _builds = builds;
        _uow = uow;
        _clock = clock;
    }

    public async Task<BuildArtifactDto> HandleAsync(RecordArtifactCommand cmd, CancellationToken cancellationToken = default)
    {
        var build = await _builds.GetByIdAsync(cmd.BuildId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Build {cmd.BuildId} not found.");

        var now = _clock.GetUtcNow();

        var artifact = build.AddArtifact(
            cmd.ArtifactId, (ArtifactKind)(int)cmd.Kind, cmd.Name, cmd.Version, cmd.Digest, cmd.SizeBytes, now);

        if (cmd.Registry is { } registry && !string.IsNullOrWhiteSpace(cmd.Reference))
        {
            build.AddPublication(
                artifactId: artifact.Id,
                publicationId: cmd.PublicationId,
                registry: (PublicationRegistry)(int)registry,
                reference: cmd.Reference!,
                tags: cmd.Tags,
                status: PublicationStatus.Pushed,
                publishedAtUtc: now);
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return artifact.ToDto();
    }
}
