using Jenkins.Contracts.Pipelines;
using Jenkins.Domain.Pipelines;

namespace Jenkins.Application.Features.Pipelines;

internal static class PipelineMapping
{
    public static PipelineDto ToDto(this Pipeline p) => new(
        Id: p.Id,
        Name: p.Name,
        Description: p.Description,
        IsActive: p.IsActive,
        CreatedAtUtc: p.CreatedAtUtc,
        Stages: p.Stages.Select(s => s.ToDto()).ToList());

    public static PipelineStageDto ToDto(this PipelineStage s) => new(
        Id: s.Id,
        Order: s.Order,
        JobName: s.JobName,
        UpstreamJobName: s.UpstreamJobName,
        Parameters: new Dictionary<string, string>(s.Parameters));
}
