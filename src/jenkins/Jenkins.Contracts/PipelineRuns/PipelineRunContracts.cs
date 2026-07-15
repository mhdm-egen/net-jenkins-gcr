namespace Jenkins.Contracts.PipelineRuns;

public enum PipelineRunStatusDto
{
    Running = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3,
}

public sealed record PipelineRunStepDto(int Order, string JobName, int BuildNumber, string Result);

public sealed record PipelineRunDto(
    Guid Id,
    Guid PipelineId,
    string PipelineName,
    Guid? RepositoryId,
    string TriggeredBy,
    PipelineRunStatusDto Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? FailureReason,
    IReadOnlyList<PipelineRunStepDto> Steps,
    string? Branch = null);

public sealed record PipelineRunSummaryDto(
    Guid Id,
    Guid PipelineId,
    string PipelineName,
    PipelineRunStatusDto Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int StepCount,
    string? FailureReason = null);

public sealed record StartPipelineRunRequest(Guid? RepositoryId, string? TriggeredBy, string? Branch = null);
