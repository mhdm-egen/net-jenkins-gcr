using Deployment.Application.Features.Environments.EditEnvironment;
using Deployment.Application.Features.Environments.ListEnvironments;
using Deployment.Application.Features.Environments.ManageFreezeWindows;
using Deployment.Application.Features.Environments.ManageTargets;
using Deployment.Application.Features.Environments.RegisterEnvironment;
using Deployment.Contracts.Environments;
using FluentValidation;
using static Deployment.Api.Endpoints.CatalogServicesEndpoints;

namespace Deployment.Api.Endpoints;

/// <summary>
/// HTTP surface for the <c>Environment</c> aggregate: registration, scalar
/// edits (rename / rank / approval / production flag), the targets editor,
/// and the freeze-window scheduler.
/// </summary>
public static class EnvironmentsEndpoints
{
    public static IEndpointRouteBuilder MapEnvironmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deployment/environments").WithTags("Environments");

        // --- Reads ---

        group.MapGet("", async (
            ListEnvironmentsHandler handler,
            CancellationToken ct) =>
        {
            return Results.Ok(await handler.HandleAsync(new ListEnvironmentsQuery(), ct));
        });

        group.MapGet("{id:guid}", async (
            Guid id,
            GetEnvironmentByIdHandler handler,
            CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetEnvironmentByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        // --- Lifecycle ---

        group.MapPost("", async (
            RegisterEnvironmentRequest body,
            RegisterEnvironmentHandler handler,
            IValidator<RegisterEnvironmentCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new RegisterEnvironmentCommand(Guid.NewGuid(),
                body.Name, body.PromotionRank, body.RequiresApproval, body.IsProduction);
            return await ValidateAndRun(validator, cmd, ct, async () =>
                Results.Created($"/api/deployment/environments/{cmd.Id}",
                    await handler.HandleAsync(cmd, ct)));
        });

        group.MapPost("{id:guid}/rename", async (
            Guid id,
            RenameEnvironmentRequest body,
            RenameEnvironmentHandler handler,
            IValidator<RenameEnvironmentCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new RenameEnvironmentCommand(id, body.Name);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/promotion-rank", async (
            Guid id,
            ChangePromotionRankRequest body,
            ChangePromotionRankHandler handler,
            IValidator<ChangePromotionRankCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new ChangePromotionRankCommand(id, body.PromotionRank);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/approval-requirement", async (
            Guid id,
            SetApprovalRequirementRequest body,
            SetApprovalRequirementHandler handler,
            IValidator<SetApprovalRequirementCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new SetApprovalRequirementCommand(id, body.RequiresApproval);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/production-flag", async (
            Guid id,
            SetProductionFlagRequest body,
            SetProductionFlagHandler handler,
            IValidator<SetProductionFlagCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new SetProductionFlagCommand(id, body.IsProduction);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        // --- Targets ---

        group.MapPost("{id:guid}/targets", async (
            Guid id,
            AddTargetRequest body,
            AddTargetHandler handler,
            IValidator<AddTargetCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new AddTargetCommand(id, Guid.NewGuid(),
                body.TargetKind, body.ResourceId, body.Region, body.Slot);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.Created(
                    $"/api/deployment/environments/{id}/targets/{cmd.TargetId}",
                    new { id = cmd.TargetId });
            });
        });

        group.MapPost("{id:guid}/targets/{targetId:guid}", async (
            Guid id,
            Guid targetId,
            UpdateTargetRequest body,
            UpdateTargetHandler handler,
            IValidator<UpdateTargetCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new UpdateTargetCommand(id, targetId,
                body.TargetKind, body.ResourceId, body.Region, body.Slot);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapDelete("{id:guid}/targets/{targetId:guid}", async (
            Guid id,
            Guid targetId,
            RemoveTargetHandler handler,
            CancellationToken ct) =>
        {
            await handler.HandleAsync(new RemoveTargetCommand(id, targetId), ct);
            return Results.NoContent();
        });

        // --- Freeze windows ---

        group.MapPost("{id:guid}/freeze-windows", async (
            Guid id,
            ScheduleFreezeWindowRequest body,
            ScheduleFreezeWindowHandler handler,
            IValidator<ScheduleFreezeWindowCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new ScheduleFreezeWindowCommand(id, Guid.NewGuid(),
                body.StartUtc, body.EndUtc, body.Reason, body.CreatedByPrincipal);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.Created(
                    $"/api/deployment/environments/{id}/freeze-windows/{cmd.FreezeWindowId}",
                    new { id = cmd.FreezeWindowId });
            });
        });

        group.MapDelete("{id:guid}/freeze-windows/{freezeWindowId:guid}", async (
            Guid id,
            Guid freezeWindowId,
            CancelFreezeWindowHandler handler,
            CancellationToken ct) =>
        {
            await handler.HandleAsync(new CancelFreezeWindowCommand(id, freezeWindowId), ct);
            return Results.NoContent();
        });

        return app;
    }
}
