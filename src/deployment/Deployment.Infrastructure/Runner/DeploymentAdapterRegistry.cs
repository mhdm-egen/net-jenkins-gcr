using Deployment.Application.Abstractions;
using Deployment.Contracts.Environments;

namespace Deployment.Infrastructure.Runner;

/// <summary>
/// Default registry: kind-specific adapters first, otherwise the fallback.
/// Built from whatever <see cref="IDeploymentAdapter"/> instances are
/// registered in DI — one per <see cref="TargetKindDto"/> wins; the explicit
/// fallback handles unmatched kinds.
/// </summary>
internal sealed class DeploymentAdapterRegistry : IDeploymentAdapterRegistry
{
    private readonly IReadOnlyDictionary<TargetKindDto, IDeploymentAdapter> _byKind;
    private readonly IDeploymentAdapter? _fallback;

    public DeploymentAdapterRegistry(
        IEnumerable<IDeploymentAdapter> adapters,
        NoOpDeploymentAdapter fallback)
    {
        // Group by kind; last registration wins (allows tests to override).
        var byKind = new Dictionary<TargetKindDto, IDeploymentAdapter>();
        foreach (var a in adapters)
        {
            if (a is NoOpDeploymentAdapter) continue; // fallback only
            byKind[a.TargetKind] = a;
        }
        _byKind = byKind;
        _fallback = fallback;
    }

    public IDeploymentAdapter Resolve(TargetKindDto kind)
    {
        if (TryResolve(kind, out var adapter) && adapter is not null) return adapter;
        throw new InvalidOperationException($"No deployment adapter registered for TargetKind={kind}.");
    }

    public bool TryResolve(TargetKindDto kind, out IDeploymentAdapter? adapter)
    {
        if (_byKind.TryGetValue(kind, out var hit))
        {
            adapter = hit;
            return true;
        }
        adapter = _fallback;
        return _fallback is not null;
    }
}
