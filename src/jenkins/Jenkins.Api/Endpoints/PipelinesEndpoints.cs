using FluentValidation;
using Jenkins.Application.Features.Pipelines;
using Jenkins.Contracts.Pipelines;
using static Jenkins.Api.Endpoints.RepositoriesEndpoints;

namespace Jenkins.Api.Endpoints;

/// <summary>
/// HTTP surface for the <c>Pipeline</c> aggregate: pipeline CRUD plus the stage
/// editor (add / update / remove / reorder). Stage mutations return the updated
/// pipeline so the UI can refresh in one round-trip.
/// </summary>
public static class PipelinesEndpoints
{
    public static IEndpointRouteBuilder MapPipelineEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jenkins/pipelines").WithTags("Pipelines");

        // --- Reads ---

        group.MapGet("", async (ListPipelinesHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListPipelinesQuery(), ct)));

        group.MapGet("{id:guid}", async (Guid id, GetPipelineByIdHandler handler, CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetPipelineByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        // --- Pipeline lifecycle ---

        group.MapPost("", async (
            CreatePipelineRequest body, CreatePipelineHandler handler,
            IValidator<CreatePipelineCommand> validator, CancellationToken ct) =>
        {
            var cmd = new CreatePipelineCommand(Guid.NewGuid(), body.Name, body.Description);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                var dto = await handler.HandleAsync(cmd, ct);
                return Results.Created($"/api/jenkins/pipelines/{dto.Id}", dto);
            });
        });

        group.MapPost("{id:guid}", async (
            Guid id, UpdatePipelineRequest body, UpdatePipelineHandler handler,
            IValidator<UpdatePipelineCommand> validator, CancellationToken ct) =>
        {
            var cmd = new UpdatePipelineCommand(id, body.Name, body.Description);
            return await ValidateAndRun(validator, cmd, ct, async () =>
                Results.Ok(await handler.HandleAsync(cmd, ct)));
        });

        group.MapPost("{id:guid}/active", async (
            Guid id, SetPipelineActiveRequest body, SetPipelineActiveHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new SetPipelineActiveCommand(id, body.IsActive), ct)));

        group.MapDelete("{id:guid}", async (Guid id, DeletePipelineHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new DeletePipelineCommand(id), ct);
            return Results.NoContent();
        });

        // --- Stage editor ---

        group.MapPost("{id:guid}/stages", async (
            Guid id, AddStageRequest body, AddStageHandler handler,
            IValidator<AddStageCommand> validator, CancellationToken ct) =>
        {
            var cmd = new AddStageCommand(id, Guid.NewGuid(), body.JobName, body.UpstreamJobName, body.Parameters);
            return await ValidateAndRun(validator, cmd, ct, async () =>
                Results.Ok(await handler.HandleAsync(cmd, ct)));
        });

        group.MapPost("{id:guid}/stages/{stageId:guid}", async (
            Guid id, Guid stageId, UpdateStageRequest body, UpdateStageHandler handler,
            IValidator<UpdateStageCommand> validator, CancellationToken ct) =>
        {
            var cmd = new UpdateStageCommand(id, stageId, body.JobName, body.UpstreamJobName, body.Parameters);
            return await ValidateAndRun(validator, cmd, ct, async () =>
                Results.Ok(await handler.HandleAsync(cmd, ct)));
        });

        group.MapDelete("{id:guid}/stages/{stageId:guid}", async (
            Guid id, Guid stageId, RemoveStageHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new RemoveStageCommand(id, stageId), ct)));

        group.MapPost("{id:guid}/stages/reorder", async (
            Guid id, ReorderStagesRequest body, ReorderStagesHandler handler,
            IValidator<ReorderStagesCommand> validator, CancellationToken ct) =>
        {
            var cmd = new ReorderStagesCommand(id, body.OrderedStageIds);
            return await ValidateAndRun(validator, cmd, ct, async () =>
                Results.Ok(await handler.HandleAsync(cmd, ct)));
        });

        return app;
    }
}
