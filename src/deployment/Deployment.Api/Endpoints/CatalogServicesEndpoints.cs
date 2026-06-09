using Deployment.Application.Features.Catalog.Services;
using Deployment.Contracts.Catalog;
using FluentValidation;

namespace Deployment.Api.Endpoints;

/// <summary>
/// HTTP surface for the <c>Service</c> aggregate. Minimal API endpoints that
/// resolve the application-layer handler from DI and project handler errors to
/// problem-details responses. FluentValidation runs inline so the failure
/// shape is consistent across endpoints without a Wolverine middleware pass.
/// </summary>
public static class CatalogServicesEndpoints
{
    public static IEndpointRouteBuilder MapCatalogServiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deployment/services").WithTags("Catalog: Services");

        group.MapGet("", async (
            bool? onlyActive,
            ListServicesHandler handler,
            CancellationToken ct) =>
        {
            var list = await handler.HandleAsync(new ListServicesQuery(onlyActive), ct);
            return Results.Ok(list);
        });

        group.MapGet("{id:guid}", async (
            Guid id,
            GetServiceByIdHandler handler,
            CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetServiceByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        group.MapPost("", async (
            RegisterServiceRequest body,
            RegisterServiceHandler handler,
            IValidator<RegisterServiceCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new RegisterServiceCommand(
                Guid.NewGuid(), body.Name, body.Kind, body.RepositoryUrl, body.TargetFramework);
            return await ValidateAndRun(validator, cmd, ct,
                async () => Results.Created($"/api/deployment/services/{cmd.Id}",
                    await handler.HandleAsync(cmd, ct)));
        });

        group.MapPost("{id:guid}/rename", async (
            Guid id,
            RenameServiceRequest body,
            RenameServiceHandler handler,
            IValidator<RenameServiceCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new RenameServiceCommand(id, body.Name);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/repository", async (
            Guid id,
            UpdateServiceRepositoryInfoRequest body,
            UpdateServiceRepositoryInfoHandler handler,
            IValidator<UpdateServiceRepositoryInfoCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new UpdateServiceRepositoryInfoCommand(id, body.RepositoryUrl, body.TargetFramework);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/deactivate", async (
            Guid id,
            ChangeServiceActivationHandler handler,
            CancellationToken ct) =>
        {
            await handler.HandleAsync(new ChangeServiceActivationCommand(id, Activate: false), ct);
            return Results.NoContent();
        });

        group.MapPost("{id:guid}/reactivate", async (
            Guid id,
            ChangeServiceActivationHandler handler,
            CancellationToken ct) =>
        {
            await handler.HandleAsync(new ChangeServiceActivationCommand(id, Activate: true), ct);
            return Results.NoContent();
        });

        return app;
    }

    internal static async Task<IResult> ValidateAndRun<TCommand>(
        IValidator<TCommand> validator,
        TCommand cmd,
        CancellationToken ct,
        Func<Task<IResult>> run)
    {
        var result = await validator.ValidateAsync(cmd, ct);
        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray());
            return Results.ValidationProblem(errors);
        }

        try
        {
            return await run();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(title: "Invalid operation", detail: ex.Message, statusCode: 409);
        }
    }
}
