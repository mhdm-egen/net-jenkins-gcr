using Metering.Application.Features.Usage;
using Metering.Contracts.Usage;

namespace Metering.Api.Endpoints;

public static class UsageEndpoints
{
    public static IEndpointRouteBuilder MapUsageEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/metering/usage").WithTags("Usage");

        // Rated AI-token usage rollup over an optional window.
        g.MapGet("summary", async (
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            GetUsageSummaryHandler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new GetUsageSummaryQuery(fromUtc, toUtc), ct)));

        // General by-meter rollup across every meter kind (AI tokens + build/deploy activity).
        g.MapGet("meters", async (
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            GetMeterTotalsHandler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new GetMeterTotalsQuery(fromUtc, toUtc), ct)));

        // AI token-usage ingest (from web-admin's AiClient). Idempotent on EventId.
        g.MapPost("ai", async (
            IngestAiUsageRequest body,
            IngestAiUsageHandler handler,
            CancellationToken ct) =>
        {
            if (body.EventId == Guid.Empty)
                return Results.BadRequest("EventId is required.");

            var ack = await handler.HandleAsync(body, ct);
            return Results.Ok(ack);
        });

        // Gauge ingest, from scheduled collectors. Separate from the AI route because a level
        // sample and a token count are different measurements, not one shape with null fields.
        g.MapPost("gauge", async (
            IngestGaugeRequest body,
            IngestGaugeHandler handler,
            CancellationToken ct) =>
        {
            if (body.EventId == Guid.Empty)
                return Results.BadRequest("EventId is required.");

            var ack = await handler.HandleAsync(body, ct);
            return ack is null
                ? Results.BadRequest(
                    $"'{body.Meter}' is not a gauge meter. Valid: NexusStorage, DockerStorage, " +
                    "CloudRunCompute, K8sResource.")
                : Results.Ok(ack);
        });

        return app;
    }
}
