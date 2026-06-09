using Deployment.Application.Features.Catalog.Applications;
using Deployment.Contracts.Catalog;
using FluentValidation;
using static Deployment.Api.Endpoints.CatalogServicesEndpoints;

namespace Deployment.Api.Endpoints;

/// <summary>
/// HTTP surface for the <c>Application</c> aggregate, including the
/// membership-editor endpoints (Add / Update / Remove ApplicationService).
/// </summary>
public static class CatalogApplicationsEndpoints
{
    public static IEndpointRouteBuilder MapCatalogApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deployment/applications").WithTags("Catalog: Applications");

        group.MapGet("", async (
            bool? onlyActive,
            ListApplicationsHandler handler,
            CancellationToken ct) =>
        {
            return Results.Ok(await handler.HandleAsync(new ListApplicationsQuery(onlyActive), ct));
        });

        group.MapGet("{id:guid}", async (
            Guid id,
            GetApplicationByIdHandler handler,
            CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetApplicationByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        group.MapPost("", async (
            RegisterApplicationRequest body,
            RegisterApplicationHandler handler,
            IValidator<RegisterApplicationCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new RegisterApplicationCommand(Guid.NewGuid(), body.Name, body.Description ?? "");
            return await ValidateAndRun(validator, cmd, ct,
                async () => Results.Created($"/api/deployment/applications/{cmd.Id}",
                    await handler.HandleAsync(cmd, ct)));
        });

        group.MapPost("{id:guid}/rename", async (
            Guid id,
            RenameApplicationRequest body,
            RenameApplicationHandler handler,
            IValidator<RenameApplicationCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new RenameApplicationCommand(id, body.Name);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/description", async (
            Guid id,
            ChangeApplicationDescriptionRequest body,
            ChangeApplicationDescriptionHandler handler,
            IValidator<ChangeApplicationDescriptionCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new ChangeApplicationDescriptionCommand(id, body.Description ?? "");
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/deactivate", async (
            Guid id,
            ChangeApplicationActivationHandler handler,
            CancellationToken ct) =>
        {
            await handler.HandleAsync(new ChangeApplicationActivationCommand(id, Activate: false), ct);
            return Results.NoContent();
        });

        group.MapPost("{id:guid}/reactivate", async (
            Guid id,
            ChangeApplicationActivationHandler handler,
            CancellationToken ct) =>
        {
            await handler.HandleAsync(new ChangeApplicationActivationCommand(id, Activate: true), ct);
            return Results.NoContent();
        });

        // --- Membership ---

        group.MapPost("{id:guid}/services", async (
            Guid id,
            AddApplicationMemberRequest body,
            AddApplicationMemberHandler handler,
            IValidator<AddApplicationMemberCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new AddApplicationMemberCommand(id, body.ServiceId, body.Role,
                body.IsOptional, body.DeploymentOrder);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/services/{serviceId:guid}", async (
            Guid id,
            Guid serviceId,
            UpdateApplicationMemberRequest body,
            UpdateApplicationMemberHandler handler,
            IValidator<UpdateApplicationMemberCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new UpdateApplicationMemberCommand(id, serviceId, body.Role,
                body.IsOptional, body.DeploymentOrder);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapDelete("{id:guid}/services/{serviceId:guid}", async (
            Guid id,
            Guid serviceId,
            RemoveApplicationMemberHandler handler,
            CancellationToken ct) =>
        {
            await handler.HandleAsync(new RemoveApplicationMemberCommand(id, serviceId), ct);
            return Results.NoContent();
        });

        return app;
    }
}
