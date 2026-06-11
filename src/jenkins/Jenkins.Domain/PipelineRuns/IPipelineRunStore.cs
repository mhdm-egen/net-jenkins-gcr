using Jenkins.Domain.Abstractions;

namespace Jenkins.Domain.PipelineRuns;

/// <summary>
/// Persistence seam for the <see cref="PipelineRun"/> aggregate. Concrete impl lives in
/// <c>Jenkins.Infrastructure.Persistence.Repositories</c>.
/// </summary>
public interface IPipelineRunStore : IRepository<PipelineRun, Guid>
{
}
