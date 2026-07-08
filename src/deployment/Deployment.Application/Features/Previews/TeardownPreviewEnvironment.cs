using Microsoft.Extensions.Logging;
using Deployment.Application.Abstractions;
using Deployment.Domain.Abstractions;
using Deployment.Domain.Previews;

namespace Deployment.Application.Features.Previews;

public sealed record TeardownPreviewEnvironmentResult(bool Applied, string Outcome);
public sealed record TeardownPreviewEnvironmentCommand(Guid PreviewId, string? TriggeredBy);

/// <summary>Tears down a preview: deletes its namespace and marks it TornDown. Idempotent — a missing
/// namespace or an already-torn-down preview succeeds. Used by both the manual teardown and the TTL sweeper.</summary>
public sealed class TeardownPreviewEnvironmentHandler
{
    private readonly IPreviewEnvironmentRepository _previews;
    private readonly INamespaceManager _namespaces;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    private readonly ILogger<TeardownPreviewEnvironmentHandler> _logger;

    public TeardownPreviewEnvironmentHandler(
        IPreviewEnvironmentRepository previews, INamespaceManager namespaces,
        IUnitOfWork uow, TimeProvider clock, ILogger<TeardownPreviewEnvironmentHandler> logger)
    {
        _previews = previews; _namespaces = namespaces; _uow = uow; _clock = clock; _logger = logger;
    }

    public async Task<TeardownPreviewEnvironmentResult> HandleAsync(TeardownPreviewEnvironmentCommand cmd, CancellationToken ct = default)
    {
        var preview = await _previews.GetByIdAsync(cmd.PreviewId, ct).ConfigureAwait(false);
        if (preview is null) return new TeardownPreviewEnvironmentResult(false, "preview-not-found");
        if (preview.Status == PreviewStatus.TornDown) return new TeardownPreviewEnvironmentResult(true, "already-torn-down");

        await _namespaces.DeleteNamespaceAsync(preview.KubeContext, preview.Namespace, ct).ConfigureAwait(false);
        preview.MarkTornDown(_clock.GetUtcNow());
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("[preview] {Preview} torn down (namespace {Namespace} deleted) by {By}.",
            preview.Id, preview.Namespace, cmd.TriggeredBy ?? "manual");
        return new TeardownPreviewEnvironmentResult(true, "torn-down");
    }
}
