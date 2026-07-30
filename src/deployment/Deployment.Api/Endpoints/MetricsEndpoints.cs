using Deployment.Application.Features.Metrics;

namespace Deployment.Api.Endpoints;

public static class MetricsEndpoints
{
    public static IEndpointRouteBuilder MapMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/deployment/metrics").WithTags("Metrics");

        // The DORA four over a trailing window. Computed server-side so the metrics page, the home
        // tile and the weekly digest cannot drift apart.
        g.MapGet("dora", async (int? days, GetDoraSummaryHandler h, CancellationToken ct) =>
            Results.Ok(await h.HandleAsync(new GetDoraSummaryQuery(days ?? 30), ct)));

        return app;
    }
}
