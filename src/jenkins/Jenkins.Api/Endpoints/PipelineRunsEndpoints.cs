using FluentValidation;
using Jenkins.Application.Features.PipelineRuns;
using Jenkins.Contracts.PipelineRuns;

namespace Jenkins.Api.Endpoints;

/// <summary>
/// HTTP surface for server-side pipeline runs: start a run, and read run status/history.
/// Live progress is streamed separately over the <c>PipelineRunHub</c> SignalR hub.
/// </summary>
public static class PipelineRunsEndpoints
{
    public static IEndpointRouteBuilder MapPipelineRunEndpoints(this IEndpointRouteBuilder app)
    {
        // Start a run of a persisted pipeline.
        app.MapPost("/api/jenkins/pipelines/{id:guid}/runs", async (
            Guid id,
            StartPipelineRunRequest body,
            StartPipelineRunHandler handler,
            IValidator<StartPipelineRunCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new StartPipelineRunCommand(id, body.RepositoryId, body.TriggeredBy);
            return await RepositoriesEndpoints.ValidateAndRun(validator, cmd, ct, async () =>
            {
                var runId = await handler.HandleAsync(cmd, ct);
                return Results.Created($"/api/jenkins/pipeline-runs/{runId}", new { id = runId });
            });
        }).WithTags("Pipeline Runs");

        var group = app.MapGroup("/api/jenkins/pipeline-runs").WithTags("Pipeline Runs");

        group.MapGet("", async (
            Guid? pipelineId,
            int? take,
            ListPipelineRunsHandler handler,
            CancellationToken ct) =>
        {
            return Results.Ok(await handler.HandleAsync(new ListPipelineRunsQuery(pipelineId, take ?? 50), ct));
        });

        group.MapGet("{id:guid}", async (
            Guid id,
            GetPipelineRunByIdHandler handler,
            CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetPipelineRunByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        return app;
    }
}
