using FluentValidation;
using Publisher.Application.Features.Registries;
using Publisher.Contracts.Registries;

namespace Publisher.Api.Endpoints;

/// <summary>
/// CRUD for the remote-registry catalog (push targets). Credentials are never accepted or returned
/// here — only a <c>CredentialSecretRef</c> name that the server resolves at push time.
/// </summary>
public static class RegistriesEndpoints
{
    public static IEndpointRouteBuilder MapRegistriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/publisher/registries").WithTags("Registries");

        group.MapGet("", async (ListRegistriesHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListRegistriesQuery(), ct)));

        group.MapGet("{id:guid}", async (Guid id, GetRegistryByIdHandler handler, CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetRegistryByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        group.MapPost("", async (
            CreateRegistryRequest body,
            CreateRegistryHandler handler,
            IValidator<CreateRegistryCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new CreateRegistryCommand(
                body.Name, body.Provider, body.RegistryHost, body.RepositoryPath,
                body.AuthMethod, body.Username, body.CredentialSecretRef, body.MakeDefault);
            return await EndpointHelpers.ValidateAndRun(validator, cmd, ct, async () =>
            {
                var dto = await handler.HandleAsync(cmd, ct);
                return Results.Created($"/api/publisher/registries/{dto.Id}", dto);
            });
        });

        group.MapPut("{id:guid}", async (
            Guid id,
            UpdateRegistryRequest body,
            UpdateRegistryHandler handler,
            IValidator<UpdateRegistryCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new UpdateRegistryCommand(
                id, body.Provider, body.RegistryHost, body.RepositoryPath,
                body.AuthMethod, body.Username, body.CredentialSecretRef);
            return await EndpointHelpers.ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/default", async (Guid id, SetDefaultRegistryHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new SetDefaultRegistryCommand(id), ct);
            return Results.NoContent();
        });

        group.MapPost("{id:guid}/enable", async (Guid id, ChangeRegistryActivationHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new ChangeRegistryActivationCommand(id, Enabled: true), ct);
            return Results.NoContent();
        });

        group.MapPost("{id:guid}/disable", async (Guid id, ChangeRegistryActivationHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new ChangeRegistryActivationCommand(id, Enabled: false), ct);
            return Results.NoContent();
        });

        group.MapDelete("{id:guid}", async (Guid id, DeleteRegistryHandler handler, CancellationToken ct) =>
        {
            try
            {
                await handler.HandleAsync(new DeleteRegistryCommand(id), ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(title: "Cannot delete registry", detail: ex.Message, statusCode: 409);
            }
        });

        return app;
    }
}
