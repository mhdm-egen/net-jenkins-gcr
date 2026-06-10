using Jenkins.Domain.Common;

namespace Jenkins.Domain.Pipelines.Events;

public sealed record PipelineCreated(
    Guid PipelineId,
    string Name,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record PipelineRenamed(
    Guid PipelineId,
    string Name,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record PipelineActivationChanged(
    Guid PipelineId,
    bool IsActive,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record PipelineStagesChanged(
    Guid PipelineId,
    int StageCount,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
