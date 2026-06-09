using Deployment.Application.Features.Deployments.BeginDeployment;
using Deployment.Application.Features.Deployments.FailDeployment;
using Deployment.Application.Features.Deployments.RecordDeploymentAudit;
using Deployment.Application.Features.Deployments.SucceedDeployment;
using FluentValidation;
using static Deployment.Api.Endpoints.CatalogServicesEndpoints;

namespace Deployment.Api.Endpoints;

/// <summary>
/// Endpoints external runners use to drive a deployment row's lifecycle.
/// The in-process runner calls the same handlers directly; this surface
/// is what a separate worker host (or a CI script) would consume.
///
/// Begin (Queued → Running), Succeed (Running → Succeeded), Fail (Running → Failed),
/// and a free-form audit append. Cascade roll-up is handled inside the
/// Succeed/Fail handlers.
/// </summary>
public static class RunnerEndpoints
{
    public static IEndpointRouteBuilder MapRunnerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deployment/deployments").WithTags("Runner");

        group.MapPost("{id:guid}/begin", async (
            Guid id,
            BeginDeploymentHandler handler,
            IValidator<BeginDeploymentCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new BeginDeploymentCommand(id);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/succeed", async (
            Guid id,
            SucceedDeploymentHandler handler,
            IValidator<SucceedDeploymentCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new SucceedDeploymentCommand(id);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/fail", async (
            Guid id,
            FailDeploymentBody body,
            FailDeploymentHandler handler,
            IValidator<FailDeploymentCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new FailDeploymentCommand(id, body.FailureReason);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/audit", async (
            Guid id,
            RecordAuditBody body,
            RecordDeploymentAuditHandler handler,
            IValidator<RecordDeploymentAuditCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new RecordDeploymentAuditCommand(id, body.EventType, body.Detail);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        return app;
    }

    // Tiny request bodies, scoped to this endpoint group.
    public sealed record FailDeploymentBody(string FailureReason);
    public sealed record RecordAuditBody(string EventType, string? Detail);
}
