using FluentValidation;
using Publisher.Application.Features.Rules;
using Publisher.Contracts.Rules;

namespace Publisher.Api.Endpoints;

/// <summary>
/// CRUD for automation rules — the opt-in "when ContainerPublished, push to remote" switches.
/// </summary>
public static class RulesEndpoints
{
    public static IEndpointRouteBuilder MapRulesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/publisher/rules").WithTags("Rules");

        group.MapGet("", async (ListRulesHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListRulesQuery(), ct)));

        group.MapGet("{id:guid}", async (Guid id, GetRuleByIdHandler handler, CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetRuleByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        group.MapPost("", async (
            CreateRuleRequest body,
            CreateRuleHandler handler,
            IValidator<CreateRuleCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new CreateRuleCommand(
                body.Name, body.Trigger, body.Action, body.TargetRegistryId,
                body.RepositoryId, body.ContainerNamePattern, body.RequirePublishable, body.RequiredChannelName);
            return await EndpointHelpers.ValidateAndRun(validator, cmd, ct, async () =>
            {
                var dto = await handler.HandleAsync(cmd, ct);
                return Results.Created($"/api/publisher/rules/{dto.Id}", dto);
            });
        });

        group.MapPut("{id:guid}", async (
            Guid id,
            UpdateRuleRequest body,
            UpdateRuleHandler handler,
            IValidator<UpdateRuleCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new UpdateRuleCommand(
                id, body.Trigger, body.Action, body.TargetRegistryId,
                body.RepositoryId, body.ContainerNamePattern, body.RequirePublishable, body.RequiredChannelName);
            return await EndpointHelpers.ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/enable", async (Guid id, ChangeRuleActivationHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new ChangeRuleActivationCommand(id, Enabled: true), ct);
            return Results.NoContent();
        });

        group.MapPost("{id:guid}/disable", async (Guid id, ChangeRuleActivationHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new ChangeRuleActivationCommand(id, Enabled: false), ct);
            return Results.NoContent();
        });

        group.MapDelete("{id:guid}", async (Guid id, DeleteRuleHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new DeleteRuleCommand(id), ct);
            return Results.NoContent();
        });

        return app;
    }
}
