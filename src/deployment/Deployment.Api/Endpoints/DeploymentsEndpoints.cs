using Deployment.Application.Features.Deployments.ApproveDeployment;
using Deployment.Application.Features.Deployments.CancelDeployment;
using Deployment.Application.Features.Deployments.GetEffectiveVersions;
using Deployment.Application.Features.Deployments.ListDeployments;
using Deployment.Application.Features.Deployments.StartDeployment;
using Deployment.Contracts.Deployments;
using FluentValidation;
using static Deployment.Api.Endpoints.CatalogServicesEndpoints;

namespace Deployment.Api.Endpoints;

/// <summary>
/// HTTP surface for the <c>Deployment</c> aggregate: list / detail; start;
/// approve / cancel; plus the Q1′ "effective versions" dashboard read.
/// </summary>
public static class DeploymentsEndpoints
{
    public static IEndpointRouteBuilder MapDeploymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deployment/deployments").WithTags("Deployments");

        // --- Reads ---

        group.MapGet("", async (
            Guid? environmentId,
            DeploymentStatusDto? status,
            Guid? releaseId,
            bool onlyParents,
            int take,
            ListDeploymentsHandler handler,
            CancellationToken ct) =>
        {
            var list = await handler.HandleAsync(
                new ListDeploymentsQuery(environmentId, status, releaseId, onlyParents, take), ct);
            return Results.Ok(list);
        });

        group.MapGet("{id:guid}", async (
            Guid id,
            GetDeploymentByIdHandler handler,
            CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetDeploymentByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        // --- Lifecycle ---

        group.MapPost("", async (
            StartDeploymentRequest body,
            StartDeploymentHandler handler,
            IValidator<StartDeploymentCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new StartDeploymentCommand(
                ReleaseId: body.ReleaseId,
                EnvironmentId: body.EnvironmentId,
                TargetIds: body.TargetIds ?? Array.Empty<Guid>(),
                Strategy: body.Strategy,
                Trigger: body.Trigger,
                TriggeredByPrincipal: body.TriggeredByPrincipal,
                SkipPromotionPathReason: body.SkipPromotionPathReason,
                OverrideFreezeReason: body.OverrideFreezeReason);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                var started = await handler.HandleAsync(cmd, ct);
                return Results.Ok(new StartedDeploymentDto(started.ParentDeploymentId, started.ChildDeploymentIds));
            });
        });

        group.MapPost("{id:guid}/approve", async (
            Guid id,
            ApproveDeploymentRequest body,
            ApproveDeploymentHandler handler,
            IValidator<ApproveDeploymentCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new ApproveDeploymentCommand(id, body.ApprovalId,
                body.ApproverPrincipal, body.Verdict, body.Comment);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/cancel", async (
            Guid id,
            CancelDeploymentRequest body,
            CancelDeploymentHandler handler,
            IValidator<CancelDeploymentCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new CancelDeploymentCommand(id, body.CancellationReason);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        // --- Q1' dashboard ---

        app.MapGet("/api/deployment/dashboards/effective-versions", async (
            Guid applicationId,
            Guid environmentId,
            GetEffectiveVersionsHandler handler,
            CancellationToken ct) =>
        {
            var ev = await handler.HandleAsync(
                new GetEffectiveVersionsQuery(applicationId, environmentId), ct);
            var rows = ev.Entries.Select(e => new EffectiveVersionRow(
                e.ServiceId, e.ServiceName, e.TargetId, e.TargetResourceId,
                e.Region, e.RunningReleaseId, e.SemanticVersion, e.CompletedAtUtc));
            return Results.Ok(rows);
        }).WithTags("Dashboards");

        return app;
    }
}
