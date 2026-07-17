namespace Jenkins.Domain.PipelineRuns;

/// <summary>
/// Persistence seam for completed-run console logs. Only appends new rows; the flush is owned by the caller's
/// <c>IUnitOfWork</c> (the executor persists console logs in the same transaction as the run settle). Kept separate
/// from <c>IPipelineRunStore</c>/<c>EfRepository</c>, which are constrained to <c>AggregateRoot</c>.
/// </summary>
public interface IPipelineRunConsoleStore
{
    void AddRange(IEnumerable<PipelineRunConsoleLog> logs);
}
