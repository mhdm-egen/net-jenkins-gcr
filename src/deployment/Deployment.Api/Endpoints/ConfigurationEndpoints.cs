using Deployment.Application.Features.Configuration.CreateConfigurationSetting;
using Deployment.Application.Features.Configuration.DeleteConfigurationSetting;
using Deployment.Application.Features.Configuration.ListConfigurationSettings;
using Deployment.Application.Features.Configuration.UpdateConfigurationSetting;
using Deployment.Contracts.Configuration;
using FluentValidation;
using static Deployment.Api.Endpoints.CatalogServicesEndpoints;

namespace Deployment.Api.Endpoints;

/// <summary>
/// HTTP surface for the <c>ConfigurationSetting</c> aggregate: per-row CRUD
/// (with a single Create that handles plain-or-secret), plus the history
/// projection read.
/// </summary>
public static class ConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deployment/configuration/settings").WithTags("Configuration");

        // --- Reads ---

        group.MapGet("", async (
            Guid deployableUnitId,
            ListConfigurationSettingsByUnitHandler handler,
            CancellationToken ct) =>
        {
            var list = await handler.HandleAsync(new ListConfigurationSettingsByUnitQuery(deployableUnitId), ct);
            return Results.Ok(list);
        });

        group.MapGet("{id:guid}", async (
            Guid id,
            GetConfigurationSettingByIdHandler handler,
            CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetConfigurationSettingByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        group.MapGet("{id:guid}/history", async (
            Guid id,
            GetConfigurationSettingHistoryHandler handler,
            CancellationToken ct) =>
        {
            var history = await handler.HandleAsync(new GetConfigurationSettingHistoryQuery(id), ct);
            return Results.Ok(history);
        });

        // --- Lifecycle ---

        group.MapPost("", async (
            CreateConfigurationSettingRequest body,
            CreateConfigurationSettingHandler handler,
            IValidator<CreateConfigurationSettingCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new CreateConfigurationSettingCommand(
                Id: Guid.NewGuid(),
                DeployableUnitId: body.DeployableUnitId,
                EnvironmentId: body.EnvironmentId,
                Key: body.Key,
                IsSecret: body.IsSecret,
                Value: body.Value,
                SecretReference: body.SecretReference,
                ValueType: body.ValueType,
                ChangedByPrincipal: body.ChangedByPrincipal);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                var id = await handler.HandleAsync(cmd, ct);
                return Results.Created($"/api/deployment/configuration/settings/{id}", new { id });
            });
        });

        group.MapPost("{id:guid}", async (
            Guid id,
            UpdateConfigurationSettingRequest body,
            UpdateConfigurationSettingHandler handler,
            IValidator<UpdateConfigurationSettingCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new UpdateConfigurationSettingCommand(
                SettingId: id,
                IsSecret: body.IsSecret,
                Value: body.Value,
                SecretReference: body.SecretReference,
                ValueType: body.ValueType,
                ChangedByPrincipal: body.ChangedByPrincipal);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        // Body-bearing DELETE (changedByPrincipal must be captured for audit).
        // Using POST .../delete keeps the body shape unambiguous.
        group.MapPost("{id:guid}/delete", async (
            Guid id,
            DeleteConfigurationSettingRequest body,
            DeleteConfigurationSettingHandler handler,
            IValidator<DeleteConfigurationSettingCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new DeleteConfigurationSettingCommand(id, body.ChangedByPrincipal);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        return app;
    }
}
