using Deployment.Application.Features.AspireApps;
using Deployment.Application.Features.Environments;
using Deployment.Domain.Abstractions;
using Deployment.Domain.Previews;

namespace Deployment.Application.Features.Previews;

public sealed record CreatePreviewEnvironmentResult(Guid PreviewId, string Namespace, string Outcome);

public sealed record CreatePreviewEnvironmentCommand(
    Guid ApplicationId, string Key, string? ManifestSource, string? Version, int? TtlHours, string? TriggeredBy);

/// <summary>
/// Stands up a preview environment for an Aspire app: derives a DNS-1123 namespace from the app name + key,
/// snapshots the manifest (the app's current, or an explicit override) and TTL, and persists a
/// <see cref="PreviewEnvironment"/> — whose creation raises <c>PreviewEnvironmentRequested</c> to drive the
/// deploy. Idempotent per app + key: a still-live preview is returned as-is.
/// </summary>
public sealed class CreatePreviewEnvironmentHandler
{
    private const int DefaultTtlHours = 24;

    private readonly IAspireApplicationReader _apps;
    private readonly IEnvironmentReader _envs;
    private readonly IPreviewEnvironmentRepository _previews;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public CreatePreviewEnvironmentHandler(
        IAspireApplicationReader apps, IEnvironmentReader envs,
        IPreviewEnvironmentRepository previews, IUnitOfWork uow, TimeProvider clock)
    {
        _apps = apps; _envs = envs; _previews = previews; _uow = uow; _clock = clock;
    }

    public async Task<CreatePreviewEnvironmentResult> HandleAsync(CreatePreviewEnvironmentCommand cmd, CancellationToken ct = default)
    {
        var app = await _apps.GetByIdAsync(cmd.ApplicationId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Aspire application not found.");
        var env = await _envs.GetByIdAsync(app.EnvironmentId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The app's environment was not found.");
        if (string.IsNullOrWhiteSpace(env.KubernetesContext) || string.IsNullOrWhiteSpace(env.KubernetesNamespace))
            throw new InvalidOperationException("The app's environment has no Kubernetes context/namespace configured.");

        var key = PreviewNaming.SlugKey(cmd.Key);
        if (key.Length == 0) throw new InvalidOperationException("A preview key (PR number or branch) is required.");

        var manifest = string.IsNullOrWhiteSpace(cmd.ManifestSource) ? app.ManifestSource : cmd.ManifestSource!.Trim();
        var version = string.IsNullOrWhiteSpace(cmd.Version) ? app.Version : cmd.Version!.Trim();
        var now = _clock.GetUtcNow();
        var ttlHours = cmd.TtlHours is > 0 ? cmd.TtlHours.Value : DefaultTtlHours;
        var expires = now.AddHours(ttlHours);

        // A still-live preview for this app + key is reused. A new manifest/version (e.g. a fresh CI publish on
        // the PR branch) redeploys it in place and extends the TTL; an unchanged one is a no-op.
        var existing = await _previews.FindLiveByAppAndKeyAsync(app.Id, key, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            var changed = !string.Equals(existing.ManifestSource, manifest, StringComparison.Ordinal)
                || !string.Equals(existing.Version, version, StringComparison.Ordinal);
            if (changed)
            {
                existing.Redeploy(manifest, version, expires, now);
                await _uow.SaveChangesAsync(ct).ConfigureAwait(false); // raises PreviewEnvironmentRequested → re-deploys
                return new CreatePreviewEnvironmentResult(existing.Id, existing.Namespace, "refreshed");
            }
            return new CreatePreviewEnvironmentResult(existing.Id, existing.Namespace, "already-exists");
        }

        var ns = PreviewNaming.Namespace(app.Name, key);
        var preview = new PreviewEnvironment(
            Guid.NewGuid(), app.Id, app.Name, key,
            env.KubernetesContext!, ns, manifest, version,
            cmd.TriggeredBy ?? "manual", now, expires);

        await _previews.AddAsync(preview, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false); // raises PreviewEnvironmentRequested → executor deploys
        return new CreatePreviewEnvironmentResult(preview.Id, ns, "requested");
    }
}
