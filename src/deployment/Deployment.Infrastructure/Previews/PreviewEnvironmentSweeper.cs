using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Deployment.Application.Features.Previews;
using Deployment.Domain.Previews;

namespace Deployment.Infrastructure.Previews;

/// <summary>
/// Periodically tears down preview environments past their TTL. Resolves a scope per sweep and reuses
/// <see cref="TeardownPreviewEnvironmentHandler"/> so manual and automatic teardown share one path.
/// </summary>
internal sealed class PreviewEnvironmentSweeper : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<PreviewOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<PreviewEnvironmentSweeper> _logger;

    public PreviewEnvironmentSweeper(
        IServiceScopeFactory scopes, IOptionsMonitor<PreviewOptions> options,
        TimeProvider clock, ILogger<PreviewEnvironmentSweeper> logger)
    {
        _scopes = scopes; _options = options; _clock = clock; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var minutes = Math.Max(1, _options.CurrentValue.SweepIntervalMinutes);
            try { await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }

            try { await SweepAsync(stoppingToken).ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogWarning(ex, "[preview] TTL sweep failed."); }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var previews = scope.ServiceProvider.GetRequiredService<IPreviewEnvironmentRepository>();
        var teardown = scope.ServiceProvider.GetRequiredService<TeardownPreviewEnvironmentHandler>();

        var expired = await previews.ListExpiredActiveAsync(_clock.GetUtcNow(), ct).ConfigureAwait(false);
        foreach (var p in expired)
        {
            _logger.LogInformation("[preview] {Preview} ({Namespace}) expired — tearing down.", p.Id, p.Namespace);
            await teardown.HandleAsync(new TeardownPreviewEnvironmentCommand(p.Id, "ttl-sweeper"), ct).ConfigureAwait(false);
        }
    }
}
