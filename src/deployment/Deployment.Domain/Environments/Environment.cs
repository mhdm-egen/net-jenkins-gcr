using Deployment.Domain.Common;
using Deployment.Domain.Environments.Events;

namespace Deployment.Domain.Environments;

/// <summary>
/// A logical deployment destination: Dev, Test, Staging, Production, etc.
/// Owns its set of <see cref="DeploymentTarget"/> instances (the concrete
/// places services run) and its <see cref="EnvironmentFreezeWindow"/>
/// schedule. Promotion ordering is driven by <see cref="PromotionRank"/>
/// (lower runs earlier).
///
/// Name conflict note: this is <c>Deployment.Domain.Environments.Environment</c>,
/// not <c>System.Environment</c>. The closer-scope rule resolves correctly
/// inside this namespace; qualify if you ever need <c>System.Environment</c>
/// nearby.
/// </summary>
public sealed class Environment : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public int PromotionRank { get; private set; }
    public bool RequiresApproval { get; private set; }
    public bool IsProduction { get; private set; }

    private readonly List<DeploymentTarget> _targets = new();
    public IReadOnlyCollection<DeploymentTarget> Targets => _targets.AsReadOnly();

    private readonly List<EnvironmentFreezeWindow> _freezeWindows = new();
    public IReadOnlyCollection<EnvironmentFreezeWindow> FreezeWindows => _freezeWindows.AsReadOnly();

    private Environment()
    {
        Name = string.Empty;
    }

    public Environment(
        Guid id,
        string name,
        int promotionRank,
        bool requiresApproval,
        bool isProduction,
        DateTimeOffset registeredAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (promotionRank < 0)
            throw new ArgumentOutOfRangeException(nameof(promotionRank), "PromotionRank cannot be negative.");

        Id = id;
        Name = name.Trim();
        PromotionRank = promotionRank;
        RequiresApproval = requiresApproval;
        IsProduction = isProduction;

        RaiseEvent(new EnvironmentRegistered(id, Name, promotionRank, requiresApproval, isProduction, registeredAtUtc));
    }

    public void Rename(string newName, DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty.", nameof(newName));
        var trimmed = newName.Trim();
        if (string.Equals(Name, trimmed, StringComparison.Ordinal)) return;
        var old = Name;
        Name = trimmed;
        RaiseEvent(new EnvironmentRenamed(Id, old, Name, occurredAtUtc));
    }

    public void ChangePromotionRank(int newRank, DateTimeOffset occurredAtUtc)
    {
        if (newRank < 0)
            throw new ArgumentOutOfRangeException(nameof(newRank), "PromotionRank cannot be negative.");
        if (PromotionRank == newRank) return;
        var old = PromotionRank;
        PromotionRank = newRank;
        RaiseEvent(new EnvironmentPromotionRankChanged(Id, old, newRank, occurredAtUtc));
    }

    public void SetApprovalRequirement(bool requiresApproval, DateTimeOffset occurredAtUtc)
    {
        if (RequiresApproval == requiresApproval) return;
        RequiresApproval = requiresApproval;
        RaiseEvent(new EnvironmentApprovalRequirementChanged(Id, requiresApproval, occurredAtUtc));
    }

    public void SetProductionFlag(bool isProduction, DateTimeOffset occurredAtUtc)
    {
        if (IsProduction == isProduction) return;
        IsProduction = isProduction;
        RaiseEvent(new EnvironmentMarkedProduction(Id, isProduction, occurredAtUtc));
    }

    // --- Targets ---

    public DeploymentTarget AddTarget(
        Guid targetId,
        TargetKind kind,
        string resourceId,
        string region,
        string? slot,
        DateTimeOffset occurredAtUtc)
    {
        if (_targets.Any(t => t.Id == targetId))
            throw new InvalidOperationException($"Target {targetId} already exists in environment {Id}.");

        var target = new DeploymentTarget(targetId, Id, kind, resourceId, region, slot);
        _targets.Add(target);
        RaiseEvent(new DeploymentTargetAdded(Id, target.Id, target.TargetKind, target.ResourceId, target.Region, target.Slot, occurredAtUtc));
        return target;
    }

    public void UpdateTarget(
        Guid targetId,
        TargetKind kind,
        string resourceId,
        string region,
        string? slot,
        DateTimeOffset occurredAtUtc)
    {
        var target = _targets.FirstOrDefault(t => t.Id == targetId)
            ?? throw new InvalidOperationException(
                $"Target {targetId} not found in environment {Id}.");
        target.Update(kind, resourceId, region, slot);
        RaiseEvent(new DeploymentTargetUpdated(Id, target.Id, target.TargetKind, target.ResourceId, target.Region, target.Slot, occurredAtUtc));
    }

    public void RemoveTarget(Guid targetId, DateTimeOffset occurredAtUtc)
    {
        var target = _targets.FirstOrDefault(t => t.Id == targetId);
        if (target is null) return;
        _targets.Remove(target);
        RaiseEvent(new DeploymentTargetRemoved(Id, targetId, occurredAtUtc));
    }

    // --- Freeze windows ---

    public EnvironmentFreezeWindow ScheduleFreezeWindow(
        Guid freezeWindowId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string reason,
        string createdByPrincipal,
        DateTimeOffset createdAtUtc)
    {
        if (_freezeWindows.Any(w => w.Id == freezeWindowId))
            throw new InvalidOperationException(
                $"FreezeWindow {freezeWindowId} already exists in environment {Id}.");

        var window = new EnvironmentFreezeWindow(
            freezeWindowId, Id, startUtc, endUtc, reason, createdByPrincipal, createdAtUtc);
        _freezeWindows.Add(window);
        RaiseEvent(new FreezeWindowScheduled(Id, window.Id, window.StartUtc, window.EndUtc, window.Reason, window.CreatedByPrincipal, createdAtUtc));
        return window;
    }

    public void CancelFreezeWindow(Guid freezeWindowId, DateTimeOffset occurredAtUtc)
    {
        var window = _freezeWindows.FirstOrDefault(w => w.Id == freezeWindowId);
        if (window is null) return;
        _freezeWindows.Remove(window);
        RaiseEvent(new FreezeWindowCancelled(Id, freezeWindowId, occurredAtUtc));
    }

    /// <summary>
    /// True if <paramref name="instant"/> falls inside any scheduled freeze
    /// window for this environment. Used by <c>StartDeployment</c> to decide
    /// whether <c>OverrideFreezeReason</c> is required.
    /// </summary>
    public bool IsFrozenAt(DateTimeOffset instant) =>
        _freezeWindows.Any(w => w.Contains(instant));
}
