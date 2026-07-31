using Cicd.Notifications;
using Deployment.Application.Features.Metrics;
using Microsoft.Extensions.Options;
using Wolverine;

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

        // Send the delivery digest now. Publishes the same message the schedule does, with Force set:
        // it bypasses the already-sent-this-week guard and does not fork a recurring chain.
        g.MapPost("digest", async (
            IMessageBus bus,
            IOptionsMonitor<NotificationOptions> notifyOptions,
            CancellationToken ct) =>
        {
            var n = notifyOptions.CurrentValue;
            // IsUsable, not Enabled: a channel switched on without its webhook/SMTP settings is
            // enabled and still cannot deliver.
            var channels = new List<string>();
            if (n.Slack.IsUsable) channels.Add("slack");
            if (n.Email.IsUsable) channels.Add("email");

            await bus.PublishAsync(new WeeklyDoraDigestDue(Force: true));

            // Report what will actually happen rather than a bare 202 — with no channel enabled, or
            // with OnlyFailures set, the digest is computed and then silently dropped, which is
            // indistinguishable from success at the call site.
            return Results.Accepted(value: new
            {
                queued = true,
                channels,
                suppressed = n.OnlyFailures,
                note = n.OnlyFailures
                    ? "Deployment:Notifications:OnlyFailures is set, so this Info-severity digest will be suppressed."
                    : channels.Count == 0
                        ? "No notification channel is enabled — the digest will be generated and then go nowhere."
                        : null,
            });
        });

        return app;
    }
}
