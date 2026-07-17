using Deployment.Application.Abstractions;
using Deployment.Contracts.Reset;
using Deployment.Domain.Previews;
using Deployment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Api.Endpoints;

/// <summary>
/// Admin "danger zone" reset: bulk-deletes selected deployment data/history. Config (services, environments,
/// mappings, Aspire app definitions) is never touched. Previews are torn down (k8s namespace) before their rows
/// are deleted so live namespaces don't leak.
/// </summary>
public static class ResetEndpoints
{
    public static IEndpointRouteBuilder MapResetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/deployment/reset", async (ResetDeploymentRequest body, ResetDeploymentHandler h, CancellationToken ct) =>
            Results.Ok(await h.HandleAsync(body, ct))).WithTags("Reset");
        return app;
    }
}

public sealed class ResetDeploymentHandler
{
    private readonly DeploymentDbContext _db;
    private readonly INamespaceManager _namespaces;
    private readonly ILogger<ResetDeploymentHandler> _logger;

    public ResetDeploymentHandler(DeploymentDbContext db, INamespaceManager namespaces, ILogger<ResetDeploymentHandler> logger)
    {
        _db = db;
        _namespaces = namespaces;
        _logger = logger;
    }

    public async Task<ResetDeploymentResultDto> HandleAsync(ResetDeploymentRequest req, CancellationToken ct)
    {
        int runs = 0, aspireRuns = 0, previews = 0, containers = 0;

        if (req.Previews)
        {
            // Tear down live namespaces first (best-effort), then drop the rows. A raw row delete would leak
            // any still-running preview namespace.
            var active = await _db.PreviewEnvironments.AsNoTracking()
                .Where(p => p.Status != PreviewStatus.TornDown)
                .Select(p => new { p.KubeContext, p.Namespace })
                .ToListAsync(ct);
            foreach (var p in active)
            {
                try { await _namespaces.DeleteNamespaceAsync(p.KubeContext, p.Namespace, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "[reset] preview namespace {Namespace} teardown failed (continuing).", p.Namespace); }
            }
            previews = await _db.PreviewEnvironments.ExecuteDeleteAsync(ct);
        }

        if (req.Runs) runs = await _db.Runs.ExecuteDeleteAsync(ct);
        if (req.AspireRuns) aspireRuns = await _db.AspireApplicationRuns.ExecuteDeleteAsync(ct);
        if (req.Containers) containers = await _db.KnownContainers.ExecuteDeleteAsync(ct);

        _logger.LogWarning("[reset] deployment data reset — runs={Runs} aspireRuns={AspireRuns} previews={Previews} containers={Containers}",
            runs, aspireRuns, previews, containers);
        return new ResetDeploymentResultDto(runs, aspireRuns, previews, containers);
    }
}
