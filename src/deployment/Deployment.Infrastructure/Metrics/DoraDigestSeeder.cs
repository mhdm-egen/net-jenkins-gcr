using Deployment.Application.Features.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Deployment.Infrastructure.Metrics;

/// <summary>
/// Starts the digest chain, and nothing more.
///
/// Deliberately NOT a timer: the recurrence lives in Wolverine's durable scheduling (see
/// <see cref="WeeklyDoraDigestHandler"/>), which survives restarts and process moves in a way a
/// process-local <c>PeriodicTimer</c> cannot. This only puts the first scheduled message on the
/// queue, then exits.
///
/// Runs on every startup, which does seed a duplicate chain each time. That is safe by design: the
/// handler's per-week marker makes a duplicate fire a no-op, and a skipping run does not reschedule,
/// so extra chains die out on their first week and the steady state is one.
/// </summary>
internal sealed class DoraDigestSeeder : BackgroundService
{
    // IMessageBus is SCOPED, and a BackgroundService is a singleton — injecting it directly is a
    // captive dependency that fails startup validation. Resolve it per use from a scope, the same way
    // PreviewEnvironmentSweeper does.
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<DoraDigestOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<DoraDigestSeeder> _logger;

    public DoraDigestSeeder(
        IServiceScopeFactory scopes, IOptionsMonitor<DoraDigestOptions> options,
        TimeProvider clock, ILogger<DoraDigestSeeder> logger)
    {
        _scopes = scopes;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            _logger.LogInformation(
                "[digest] Weekly delivery digest is disabled (Deployment:DoraDigest:Enabled). " +
                "The manual trigger still works.");
            return;
        }

        // Let the broker and message store finish coming up before scheduling onto them.
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        try
        {
            var delay = options.UntilNext(_clock.GetUtcNow());
            using var scope = _scopes.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.ScheduleAsync(new WeeklyDoraDigestDue(), delay).ConfigureAwait(false);
            _logger.LogInformation(
                "[digest] Weekly digest seeded — first send in {Days:0.0} day(s) ({Day} {Hour:00}:00 UTC).",
                delay.TotalDays, options.DayOfWeek, options.HourUtc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[digest] Could not seed the weekly digest schedule.");
        }
    }
}
