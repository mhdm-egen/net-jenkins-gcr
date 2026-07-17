using Jenkins.Application.Features.Repositories;
using Jenkins.Contracts.Repositories;
using Jenkins.Contracts.Seed;
using Jenkins.Domain.SourceRepositories;

namespace Jenkins.Api.Endpoints;

/// <summary>
/// Admin "demo setup" seed for CI: registers the demo tracked repositories (and, for the Cloud Run
/// scenario, the container→component mapping) so an operator can trigger real pipeline runs. Additive
/// + idempotent (find-by-name skip). The inverse of the CI-history reset. Names/URLs mirror the real
/// bundled demo repos — <c>sample-aspire</c> (a dedicated Aspire app repo, AppHost auto-discovered) and
/// <c>Web App</c> (the monorepo, standard cicd-build) — so on an unmodified system both are skipped.
/// Pipelines ("CICD Main" / "Aspire build") are already auto-seeded at startup. Registering a
/// repository raises only a config event with no consumer — no build/deploy is triggered.
/// </summary>
public static class CiSeedEndpoints
{
    public static IEndpointRouteBuilder MapCiSeedEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/jenkins/ci/seed-demo", async (SeedDemoCiRequest body, SeedCiHandler h, CancellationToken ct) =>
            Results.Ok(await h.HandleAsync(body, ct))).WithTags("Seed");
        return app;
    }
}

public sealed class SeedCiHandler
{
    // Grounded demo constants — mirror the real registered demo repos.
    private const string BaseVersion = "1.0.0";

    // Aspire: a DEDICATED repo whose AppHost is at the repo root (AppHostPath null → aspirate auto-discovers).
    private const string AspireRepoName = "sample-aspire";
    private const string AspireGitUrl = "https://github.com/mhdm-egen/sample-aspire.git";
    private const string AspireJob = "cicd-aspire-publish";

    // Standard: the monorepo, built by cicd-build; publishes the 'webapphost' container.
    private const string StandardRepoName = "Web App";
    private const string StandardGitUrl = "https://github.com/mhdm-egen/net-jenkins-gcr.git";
    private const string StandardJob = "cicd-build";

    private readonly RegisterRepositoryHandler _register;
    private readonly MapComponentHandler _mapComponent;
    private readonly ISourceRepositoryStore _repos;
    private readonly ILogger<SeedCiHandler> _logger;

    public SeedCiHandler(RegisterRepositoryHandler register, MapComponentHandler mapComponent, ISourceRepositoryStore repos, ILogger<SeedCiHandler> logger)
    {
        _register = register;
        _mapComponent = mapComponent;
        _repos = repos;
        _logger = logger;
    }

    public async Task<SeedCiResultDto> HandleAsync(SeedDemoCiRequest req, CancellationToken ct)
    {
        var items = new List<SeedCiItemDto>();
        var log = new List<string>();
        void Record(string kind, string name, bool created)
        {
            items.Add(new SeedCiItemDto(kind, name, created ? "created" : "skipped"));
            log.Add($"{(created ? "created" : "skipped")} {kind}: {name}");
        }

        if (req.AspireRepo)
            await EnsureRepoAsync(AspireRepoName, AspireGitUrl, AspireJob, BuildKindDto.Aspire, appHostPath: null, Record, ct);

        if (req.CloudRunRepo)
        {
            var repoId = await EnsureRepoAsync(StandardRepoName, StandardGitUrl, StandardJob, BuildKindDto.Standard, appHostPath: null, Record, ct);

            // Wire the container→deployment-component mapping so a published container auto-hands-off
            // (makes the deployment mapping's auto-deploy flag functional end-to-end).
            if (req.DeployableUnitId is { } unitId && !string.IsNullOrWhiteSpace(req.ContainerName))
            {
                var repo = await _repos.FindByNameAsync(StandardRepoName, ct);
                var already = repo?.Components.Any(c => string.Equals(c.ContainerName, req.ContainerName, StringComparison.OrdinalIgnoreCase)) == true;
                if (already) Record("component", req.ContainerName!, created: false);
                else
                {
                    await _mapComponent.HandleAsync(new MapComponentCommand(
                        repoId, Guid.NewGuid(), req.ContainerName!, unitId,
                        string.IsNullOrWhiteSpace(req.DeployableUnitName) ? req.ContainerName! : req.DeployableUnitName!,
                        AutoPublish: true), ct);
                    Record("component", req.ContainerName!, created: true);
                }
            }
            else
            {
                log.Add("component mapping skipped — no deployment service provided");
            }
        }

        var created = items.Count(i => i.Status == "created");
        var skipped = items.Count(i => i.Status == "skipped");
        _logger.LogInformation("[seed] CI demo config seed — created={Created} skipped={Skipped}", created, skipped);
        return new SeedCiResultDto(created, skipped, items, log);
    }

    private async Task<Guid> EnsureRepoAsync(string name, string gitUrl, string ciJob, BuildKindDto buildKind, string? appHostPath, Action<string, string, bool> record, CancellationToken ct)
    {
        var existing = await _repos.FindByNameAsync(name, ct);
        if (existing is not null) { record("repository", name, false); return existing.Id; }
        var dto = await _register.HandleAsync(new RegisterRepositoryCommand(
            Guid.NewGuid(), name, gitUrl, RepositoryProviderDto.GitHub,
            DefaultBranch: "main", CiJobName: ciJob, BaseVersion: BaseVersion,
            BuildKind: buildKind, AppHostPath: appHostPath), ct);
        record("repository", name, true);
        return dto.Id;
    }
}
