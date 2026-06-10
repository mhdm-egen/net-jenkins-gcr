using FluentValidation;
using Jenkins.Application.Features.Builds;
using Jenkins.Contracts.Builds;
using static Jenkins.Api.Endpoints.RepositoriesEndpoints;

namespace Jenkins.Api.Endpoints;

/// <summary>
/// HTTP surface for the <c>Build</c> aggregate: record (ingest), complete, record
/// artifacts, list-by-repository, and detail. The record/complete/artifact trio is
/// what the Jenkins sync path drives as a build progresses.
/// </summary>
public static class BuildsEndpoints
{
    public static IEndpointRouteBuilder MapBuildEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jenkins/builds").WithTags("Builds");

        // --- Reads ---

        group.MapGet("", async (
            Guid repositoryId,
            int? take,
            ListBuildsHandler handler,
            CancellationToken ct) =>
        {
            var list = await handler.HandleAsync(new ListBuildsQuery(repositoryId, take ?? 50), ct);
            return Results.Ok(list);
        });

        group.MapGet("{id:guid}", async (
            Guid id,
            GetBuildByIdHandler handler,
            CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetBuildByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        // --- Ingestion lifecycle ---

        group.MapPost("", async (
            RecordBuildRequest body,
            RecordBuildHandler handler,
            IValidator<RecordBuildCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new RecordBuildCommand(
                Guid.NewGuid(), body.RepositoryId, body.CiJobName, body.CiBuildNumber,
                body.CiRunUrl, body.CiRunId, body.CommitSha, body.CommitShort, body.Branch,
                body.Author, body.Message, body.CommittedAtUtc, body.TriggeredBy, body.StartedAtUtc);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                var dto = await handler.HandleAsync(cmd, ct);
                return Results.Created($"/api/jenkins/builds/{dto.Id}", dto);
            });
        });

        group.MapPost("{id:guid}/complete", async (
            Guid id,
            CompleteBuildRequest body,
            CompleteBuildHandler handler,
            IValidator<CompleteBuildCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new CompleteBuildCommand(
                id, body.Status, body.CompletedAtUtc, body.DurationMs,
                body.Versions is null
                    ? null
                    : new BuildVersionsInput(body.Versions.PackageVersion, body.Versions.FileVersion,
                        body.Versions.AssemblyVersion, body.Versions.InformationalVersion, body.Versions.BaseVersion),
                body.Quality is null
                    ? null
                    : new BuildQualityInput(body.Quality.SbomUri, body.Quality.VulnerabilityReportUri));
            return await ValidateAndRun(validator, cmd, ct, async () =>
                Results.Ok(await handler.HandleAsync(cmd, ct)));
        });

        group.MapPost("{id:guid}/artifacts", async (
            Guid id,
            RecordArtifactRequest body,
            RecordArtifactHandler handler,
            IValidator<RecordArtifactCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new RecordArtifactCommand(
                id, Guid.NewGuid(), body.Kind, body.Name, body.Version, body.Digest, body.SizeBytes,
                body.Registry, body.Reference, body.Tags, Guid.NewGuid());
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                var dto = await handler.HandleAsync(cmd, ct);
                return Results.Created($"/api/jenkins/builds/{id}/artifacts/{dto.Id}", dto);
            });
        });

        return app;
    }
}
