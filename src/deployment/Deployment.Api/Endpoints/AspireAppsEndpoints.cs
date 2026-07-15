using FluentValidation;
using Deployment.Application.Features.AspireApps;
using Deployment.Contracts.AspireApps;

namespace Deployment.Api.Endpoints;

public static class AspireAppsEndpoints
{
    public static IEndpointRouteBuilder MapAspireAppEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/deployment/aspire-apps").WithTags("AspireApps");

        g.MapGet("", async (ListAspireApplicationsHandler h, CancellationToken ct) => Results.Ok(await h.HandleAsync(new ListAspireApplicationsQuery(), ct)));
        g.MapGet("{id:guid}", async (Guid id, GetAspireApplicationByIdHandler h, CancellationToken ct) =>
            await h.HandleAsync(new GetAspireApplicationByIdQuery(id), ct) is { } d ? Results.Ok(d) : Results.NotFound());

        g.MapPost("", async (CreateAspireApplicationRequest body, CreateAspireApplicationHandler h, IValidator<CreateAspireApplicationCommand> v, CancellationToken ct) =>
        {
            var cmd = new CreateAspireApplicationCommand(body.Name, body.Description, body.EnvironmentId, body.ManifestSource, body.Version, body.SourceKey, body.MainBranch, (Deployment.Domain.Mappings.RolloutStrategy)(int)body.Strategy, (Deployment.Domain.Mappings.PromotionMode)(int)body.PromotionMode);
            return await EndpointHelpers.ValidateAndRun(v, cmd, ct, async () =>
            {
                var dto = await h.HandleAsync(cmd, ct);
                return Results.Created($"/api/deployment/aspire-apps/{dto.Id}", dto);
            });
        });

        g.MapPut("{id:guid}", async (Guid id, UpdateAspireApplicationRequest body, UpdateAspireApplicationHandler h, IValidator<UpdateAspireApplicationCommand> v, CancellationToken ct) =>
        {
            var cmd = new UpdateAspireApplicationCommand(id, body.Name, body.Description, body.EnvironmentId, body.ManifestSource, body.Version, body.SourceKey, body.MainBranch, (Deployment.Domain.Mappings.RolloutStrategy)(int)body.Strategy, (Deployment.Domain.Mappings.PromotionMode)(int)body.PromotionMode);
            return await EndpointHelpers.ValidateAndRun(v, cmd, ct, async () => { await h.HandleAsync(cmd, ct); return Results.NoContent(); });
        });

        g.MapDelete("{id:guid}", async (Guid id, DeleteAspireApplicationHandler h, CancellationToken ct) =>
        {
            try { await h.HandleAsync(new DeleteAspireApplicationCommand(id), ct); return Results.NoContent(); }
            catch (InvalidOperationException ex) { return Results.Problem(title: "Cannot delete", detail: ex.Message, statusCode: 409); }
        });

        g.MapPost("{id:guid}/deploy", async (Guid id, TriggerAspireDeploymentRequest body, RequestAspireDeploymentHandler h, CancellationToken ct) =>
        {
            var result = await h.HandleAsync(new RequestAspireDeploymentCommand(id, body.TriggeredBy), ct);
            return result.RunId is { } ? Results.Accepted($"/api/deployment/aspire-runs/{result.RunId}", result) : Results.Ok(result);
        });

        g.MapPost("{id:guid}/auto-deploy", async (Guid id, SetAspireAutoDeployRequest body, SetAspireAutoDeployHandler h, CancellationToken ct) =>
        {
            try { await h.HandleAsync(new SetAspireAutoDeployCommand(id, body.AutoDeploy), ct); return Results.NoContent(); }
            catch (InvalidOperationException ex) { return Results.Problem(title: "Not found", detail: ex.Message, statusCode: 404); }
        });

        g.MapPost("{id:guid}/rollback", async (Guid id, RollbackAspireDeploymentRequest body, RollbackAspireDeploymentHandler h, CancellationToken ct) =>
        {
            var result = await h.HandleAsync(new RollbackAspireDeploymentCommand(id, body.TargetRunId, body.TriggeredBy), ct);
            return result.RunId is { } ? Results.Accepted($"/api/deployment/aspire-runs/{result.RunId}", result) : Results.Ok(result);
        });

        g.MapPost("{id:guid}/promote", async (Guid id, PromoteAspireDeploymentRequest body, PromoteAspireDeploymentHandler h, CancellationToken ct) =>
        {
            var result = await h.HandleAsync(new PromoteAspireDeploymentCommand(id, body.TargetEnvironmentId, body.TriggeredBy), ct);
            return result.RunId is { } ? Results.Accepted($"/api/deployment/aspire-runs/{result.RunId}", result) : Results.Ok(result);
        });

        g.MapGet("{id:guid}/status", async (Guid id, GetAspireAppStatusHandler h, CancellationToken ct) =>
            await h.HandleAsync(new GetAspireAppStatusQuery(id), ct) is { } d ? Results.Ok(d) : Results.NotFound());

        return app;
    }

    public static IEndpointRouteBuilder MapAspireRunEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/deployment/aspire-runs").WithTags("AspireRuns");

        g.MapGet("", async (Guid? applicationId, ListAspireRunsHandler h, CancellationToken ct) =>
            Results.Ok(await h.HandleAsync(new ListAspireRunsQuery(applicationId), ct)));
        g.MapGet("{id:guid}", async (Guid id, GetAspireRunByIdHandler h, CancellationToken ct) =>
            await h.HandleAsync(new GetAspireRunByIdQuery(id), ct) is { } d ? Results.Ok(d) : Results.NotFound());

        g.MapPost("{id:guid}/approve", async (Guid id, ApproveAspireRunRequest body, ApproveAspireRunHandler h, CancellationToken ct) =>
        {
            var r = await h.HandleAsync(new ApproveAspireRunCommand(id, body.ApprovedBy), ct);
            return r.Applied ? Results.Ok(r) : Results.Problem(title: "Cannot approve", detail: r.Outcome, statusCode: 409);
        });

        g.MapPost("{id:guid}/reject", async (Guid id, RejectAspireRunRequest body, RejectAspireRunHandler h, CancellationToken ct) =>
        {
            var r = await h.HandleAsync(new RejectAspireRunCommand(id, body.RejectedBy, body.Reason), ct);
            return r.Applied ? Results.Ok(r) : Results.Problem(title: "Cannot reject", detail: r.Outcome, statusCode: 409);
        });

        // Blue-green manual promotion: promote (cut over to green, delete old namespace) or roll back (delete
        // green) a run parked in AwaitingPromotion.
        g.MapPost("{id:guid}/promote", async (Guid id, PromoteAspireRunRequest? body, PromoteAspireRunHandler h, CancellationToken ct) =>
        {
            var r = await h.HandleAsync(new PromoteAspireRunCommand(id, body?.PromotedBy), ct);
            return r.Applied ? Results.Ok(r) : Results.Problem(title: "Cannot promote", detail: r.Outcome, statusCode: 409);
        });
        g.MapPost("{id:guid}/rollback", async (Guid id, RollbackAspireRunRequest? body, RollbackAspireRunHandler h, CancellationToken ct) =>
        {
            var r = await h.HandleAsync(new RollbackAspireRunCommand(id, body?.RolledBackBy, body?.Reason), ct);
            return r.Applied ? Results.Ok(r) : Results.Problem(title: "Cannot roll back", detail: r.Outcome, statusCode: 409);
        });

        return app;
    }
}
