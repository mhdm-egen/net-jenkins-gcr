namespace Jenkins.Contracts.Pipelines;

// --- Read-side DTOs ---

public sealed record PipelineSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    int StageCount);

public sealed record PipelineStageDto(
    Guid Id,
    int Order,
    string JobName,
    string? UpstreamJobName,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record PipelineDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<PipelineStageDto> Stages);

// --- Write-side requests ---

public sealed record CreatePipelineRequest(string Name, string? Description);

public sealed record UpdatePipelineRequest(string Name, string? Description);

public sealed record SetPipelineActiveRequest(bool IsActive);

public sealed record AddStageRequest(
    string JobName,
    string? UpstreamJobName,
    IReadOnlyDictionary<string, string>? Parameters);

public sealed record UpdateStageRequest(
    string JobName,
    string? UpstreamJobName,
    IReadOnlyDictionary<string, string>? Parameters);

public sealed record ReorderStagesRequest(IReadOnlyList<Guid> OrderedStageIds);
