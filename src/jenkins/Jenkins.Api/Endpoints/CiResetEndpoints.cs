using Jenkins.Client;
using Jenkins.Contracts.Reset;
using Jenkins.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jenkins.Api.Endpoints;

/// <summary>
/// Admin "danger zone" reset for CI history. Wipes the build mirror + pipeline-run history and — when selected —
/// prunes the builds on the real Jenkins server so the mirror wipe sticks (otherwise the sync re-ingests them).
/// Pipeline/job definitions and tracked repositories are never touched.
/// </summary>
public static class CiResetEndpoints
{
    public static IEndpointRouteBuilder MapCiResetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/jenkins/ci/reset", async (ResetCiRequest body, ResetCiHistoryHandler h, CancellationToken ct) =>
            Results.Ok(await h.HandleAsync(body, ct))).WithTags("Reset");
        return app;
    }
}

public sealed class ResetCiHistoryHandler
{
    private readonly JenkinsCiDbContext _db;
    private readonly IServiceProvider _sp;
    private readonly ILogger<ResetCiHistoryHandler> _logger;

    public ResetCiHistoryHandler(JenkinsCiDbContext db, IServiceProvider sp, ILogger<ResetCiHistoryHandler> logger)
    {
        _db = db;
        _sp = sp;
        _logger = logger;
    }

    public async Task<CiResetResultDto> HandleAsync(ResetCiRequest req, CancellationToken ct)
    {
        var jenkinsBuildsDeleted = 0;
        var jobsPruned = 0;

        // Prune the real Jenkins server first so the mirror wipe sticks. Best-effort — needs the token to have
        // Run/Delete; a 403/404 on any build is logged and skipped. Only runs when Jenkins is configured.
        if (req.PruneJenkinsServer && _sp.GetService<IJenkinsClient>() is { } jenkins)
        {
            var repoJobs = await _db.Repositories.AsNoTracking().Select(r => r.CiJobName).ToListAsync(ct);
            var stageJobs = await _db.PipelineStages.AsNoTracking().Select(s => s.JobName).ToListAsync(ct);
            var upstreamJobs = await _db.PipelineStages.AsNoTracking()
                .Where(s => s.UpstreamJobName != null).Select(s => s.UpstreamJobName!).ToListAsync(ct);

            var jobs = repoJobs.Concat(stageJobs).Concat(upstreamJobs)
                .Where(j => !string.IsNullOrWhiteSpace(j))
                .Select(j => j.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var job in jobs)
            {
                try
                {
                    var builds = await jenkins.ListBuildsAsync(job, 1000, ct);
                    if (builds.Count == 0) continue;
                    jobsPruned++;
                    foreach (var b in builds)
                    {
                        try { await jenkins.DeleteBuildAsync(job, b.Number, ct); jenkinsBuildsDeleted++; }
                        catch (Exception ex) { _logger.LogWarning(ex, "[reset] delete build {Job}#{Number} on Jenkins failed (continuing).", job, b.Number); }
                    }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "[reset] pruning job {Job} on Jenkins failed (continuing).", job); }
            }
        }

        var builds2 = 0;
        var pipelineRuns = 0;

        if (req.BuildHistory)
        {
            // Child-first (Handoff / PipelineRun have no cascade; artifacts/publications cascade from Build but
            // delete explicitly so order is unambiguous).
            await _db.ArtifactPublications.ExecuteDeleteAsync(ct);
            await _db.BuildArtifacts.ExecuteDeleteAsync(ct);
            builds2 = await _db.Builds.ExecuteDeleteAsync(ct);
            await _db.Handoffs.ExecuteDeleteAsync(ct);
        }

        if (req.PipelineRuns)
        {
            await _db.PipelineRunConsoleLogs.ExecuteDeleteAsync(ct);
            pipelineRuns = await _db.PipelineRuns.ExecuteDeleteAsync(ct);
        }

        _logger.LogWarning("[reset] CI history reset — builds={Builds} pipelineRuns={Runs} jenkinsBuildsDeleted={JD} jobsPruned={JP}",
            builds2, pipelineRuns, jenkinsBuildsDeleted, jobsPruned);
        return new CiResetResultDto(builds2, pipelineRuns, jenkinsBuildsDeleted, jobsPruned);
    }
}
