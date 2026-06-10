using FluentValidation;
using Jenkins.Application.Features.Handoffs;
using Jenkins.Contracts.Handoffs;
using static Jenkins.Api.Endpoints.RepositoriesEndpoints;

namespace Jenkins.Api.Endpoints;

/// <summary>
/// HTTP surface for the <c>ContainerReleaseHandoff</c> aggregate: promote a green
/// container build to a deployment Release (the CI→deployment seam), plus list and
/// detail. Promotion is operator-driven by default (decision #3).
/// </summary>
public static class HandoffsEndpoints
{
    public static IEndpointRouteBuilder MapHandoffEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jenkins/handoffs").WithTags("Handoffs");

        // --- Reads ---

        group.MapGet("", async (
            Guid buildId,
            ListHandoffsByBuildHandler handler,
            CancellationToken ct) =>
        {
            return Results.Ok(await handler.HandleAsync(new ListHandoffsByBuildQuery(buildId), ct));
        });

        group.MapGet("{id:guid}", async (
            Guid id,
            GetHandoffByIdHandler handler,
            CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetHandoffByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        // --- Promote (CI → deployment) ---

        group.MapPost("", async (
            PromoteBuildRequest body,
            PromoteToReleaseHandler handler,
            IValidator<PromoteToReleaseCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new PromoteToReleaseCommand(body.BuildId, body.BuildArtifactId, body.RequestedByPrincipal);
            return await ValidateAndRun(validator, cmd, ct, async () =>
            {
                var dto = await handler.HandleAsync(cmd, ct);
                return Results.Created($"/api/jenkins/handoffs/{dto.Id}", dto);
            });
        });

        return app;
    }
}
