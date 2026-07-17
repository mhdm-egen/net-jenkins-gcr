using Jenkins.Domain.PipelineRuns;

namespace Jenkins.Infrastructure.Persistence.Repositories;

/// <summary>
/// Tracks completed-run console logs on the same scoped <see cref="JenkinsCiDbContext"/> the executor's
/// <c>IUnitOfWork</c> wraps, so they flush in the settle transaction. Append-only (see <see cref="IPipelineRunConsoleStore"/>).
/// </summary>
public sealed class PipelineRunConsoleStore : IPipelineRunConsoleStore
{
    private readonly JenkinsCiDbContext _db;

    public PipelineRunConsoleStore(JenkinsCiDbContext db) => _db = db;

    public void AddRange(IEnumerable<PipelineRunConsoleLog> logs)
        => _db.PipelineRunConsoleLogs.AddRange(logs);
}
