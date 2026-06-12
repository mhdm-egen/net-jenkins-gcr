using Publisher.Domain.Registries;

namespace Publisher.Application.Features.Promotions;

/// <summary>
/// Manual (API-triggered) promotion of a container. Resolves the target registry — the one
/// specified, or the configured default when omitted — then delegates to
/// <see cref="PromoteContainerHandler"/> (same path the rules use).
/// </summary>
public sealed record RequestManualPromotionCommand(Guid ContainerId, Guid? RegistryId, string? TriggeredBy);

public sealed class RequestManualPromotionHandler
{
    private readonly IRemoteRegistryRepository _registries;
    private readonly PromoteContainerHandler _promote;

    public RequestManualPromotionHandler(IRemoteRegistryRepository registries, PromoteContainerHandler promote)
    {
        _registries = registries;
        _promote = promote;
    }

    public async Task<PromoteResult> HandleAsync(RequestManualPromotionCommand cmd, CancellationToken cancellationToken = default)
    {
        var registryId = cmd.RegistryId;
        if (registryId is null)
        {
            var def = await _registries.FindDefaultAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("No registry specified and no default registry is configured.");
            registryId = def.Id;
        }

        return await _promote.HandleAsync(
            new PromoteContainerCommand(cmd.ContainerId, registryId.Value, RuleId: null, cmd.TriggeredBy ?? "manual"),
            cancellationToken).ConfigureAwait(false);
    }
}
