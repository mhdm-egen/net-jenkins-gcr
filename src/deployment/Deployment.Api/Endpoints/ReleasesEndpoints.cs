using Deployment.Application.Features.Releases.AttachProvenance;
using Deployment.Application.Features.Releases.ChangeReleaseStatus;
using Deployment.Application.Features.Releases.ListReleases;
using Deployment.Application.Features.Releases.ManageComposition;
using Deployment.Application.Features.Releases.PublishRelease;
using Deployment.Contracts.Releases;
using FluentValidation;
using static Deployment.Api.Endpoints.CatalogServicesEndpoints;

namespace Deployment.Api.Endpoints;

/// <summary>
/// HTTP surface for the <c>Release</c> aggregate: publish, list, detail,
/// status changes, provenance attachment, and the BOM (composition) editor
/// for Application releases.
/// </summary>
public static class ReleasesEndpoints
{
    public static IEndpointRouteBuilder MapReleaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deployment/releases").WithTags("Releases");

        // --- Reads ---

        group.MapGet("", async (
            Guid deployableUnitId,
            ListReleasesByUnitHandler handler,
            CancellationToken ct) =>
        {
            var list = await handler.HandleAsync(new ListReleasesByUnitQuery(deployableUnitId), ct);
            return Results.Ok(list);
        });

        group.MapGet("{id:guid}", async (
            Guid id,
            GetReleaseByIdHandler handler,
            CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetReleaseByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        group.MapGet("{id:guid}/status-history", async (
            Guid id,
            GetReleaseStatusHistoryHandler handler,
            CancellationToken ct) =>
        {
            var history = await handler.HandleAsync(new GetReleaseStatusHistoryQuery(id), ct);
            return Results.Ok(history);
        });

        // --- Lifecycle ---

        group.MapPost("", async (
            PublishReleaseRequest body,
            PublishReleaseHandler handler,
            IValidator<PublishReleaseCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new PublishReleaseCommand(
                Id: Guid.NewGuid(),
                DeployableUnitId: body.DeployableUnitId,
                SemanticVersion: body.SemanticVersion,
                BuildNumber: body.BuildNumber,
                CommitSha: body.CommitSha,
                ArtifactType: body.ArtifactType,
                ArtifactUri: body.ArtifactUri);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                var id = await handler.HandleAsync(cmd, ct);
                return Results.Created($"/api/deployment/releases/{id}", new { id });
            });
        });

        group.MapPost("{id:guid}/provenance", async (
            Guid id,
            AttachProvenanceRequest body,
            AttachProvenanceHandler handler,
            IValidator<AttachProvenanceCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new AttachProvenanceCommand(id,
                body.ArtifactSha256, body.SbomUri, body.VulnerabilityReportUri,
                body.CiRunUrl, body.CiRunId, body.PublishedByPrincipal);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/status", async (
            Guid id,
            ChangeReleaseStatusRequest body,
            ChangeReleaseStatusHandler handler,
            IValidator<ChangeReleaseStatusCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new ChangeReleaseStatusCommand(id, body.NewStatus, body.Reason, body.ChangedByPrincipal);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        // --- Composition (BOM) editor — Application releases ---

        group.MapPost("{id:guid}/compositions", async (
            Guid id,
            AddCompositionEntryRequest body,
            AddCompositionEntryHandler handler,
            IValidator<AddCompositionEntryCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new AddCompositionEntryCommand(id, body.ServiceId, body.PinMode, body.ServiceReleaseId);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapPost("{id:guid}/compositions/{serviceId:guid}", async (
            Guid id,
            Guid serviceId,
            UpdateCompositionEntryRequest body,
            UpdateCompositionEntryHandler handler,
            IValidator<UpdateCompositionEntryCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new UpdateCompositionEntryCommand(id, serviceId, body.PinMode, body.ServiceReleaseId);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.NoContent();
            });
        });

        group.MapDelete("{id:guid}/compositions/{serviceId:guid}", async (
            Guid id,
            Guid serviceId,
            RemoveCompositionEntryHandler handler,
            CancellationToken ct) =>
        {
            await handler.HandleAsync(new RemoveCompositionEntryCommand(id, serviceId), ct);
            return Results.NoContent();
        });

        return app;
    }
}
