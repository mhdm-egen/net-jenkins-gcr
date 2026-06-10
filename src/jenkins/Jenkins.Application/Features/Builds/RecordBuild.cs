using Jenkins.Contracts.Builds;
using Jenkins.Domain.Abstractions;
using Jenkins.Domain.Builds;
using FluentValidation;

namespace Jenkins.Application.Features.Builds;

/// <summary>
/// Ingest a build observed from Jenkins, in <c>Running</c> state. Idempotent on the
/// CI key (job + number): a repeat returns the existing build rather than failing —
/// the Jenkins sync path can call this freely.
/// </summary>
public sealed record RecordBuildCommand(
    Guid Id,
    Guid RepositoryId,
    string CiJobName,
    int CiBuildNumber,
    string CiRunUrl,
    string CiRunId,
    string CommitSha,
    string CommitShort,
    string Branch,
    string? Author,
    string? Message,
    DateTimeOffset? CommittedAtUtc,
    string? TriggeredBy,
    DateTimeOffset StartedAtUtc);

public sealed class RecordBuildValidator : AbstractValidator<RecordBuildCommand>
{
    public RecordBuildValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.CiJobName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CiBuildNumber).GreaterThan(0);
        RuleFor(x => x.CiRunUrl).NotEmpty().MaximumLength(500);
        RuleFor(x => x.CiRunId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CommitSha).NotEmpty().MaximumLength(64);
        RuleFor(x => x.CommitShort).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Branch).NotEmpty().MaximumLength(200);
    }
}

public sealed class RecordBuildHandler
{
    private readonly IBuildStore _builds;
    private readonly IUnitOfWork _uow;

    public RecordBuildHandler(IBuildStore builds, IUnitOfWork uow)
    {
        _builds = builds;
        _uow = uow;
    }

    public async Task<BuildSummaryDto> HandleAsync(RecordBuildCommand cmd, CancellationToken cancellationToken = default)
    {
        var existing = await _builds.FindByCiKeyAsync(cmd.CiJobName, cmd.CiBuildNumber, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return existing.ToSummaryDto();

        var revision = new SourceRevision(
            cmd.CommitSha, cmd.CommitShort, cmd.Branch, cmd.Author, cmd.Message, cmd.CommittedAtUtc);

        var build = new Build(
            id: cmd.Id,
            repositoryId: cmd.RepositoryId,
            ciJobName: cmd.CiJobName,
            ciBuildNumber: cmd.CiBuildNumber,
            ciRunUrl: cmd.CiRunUrl,
            ciRunId: cmd.CiRunId,
            sourceRevision: revision,
            triggeredBy: cmd.TriggeredBy,
            startedAtUtc: cmd.StartedAtUtc);

        await _builds.AddAsync(build, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return build.ToSummaryDto();
    }
}
