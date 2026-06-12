using Microsoft.Extensions.Logging;
using Publisher.Application.Abstractions;
using Publisher.Domain.Abstractions;
using Publisher.Domain.Promotions;
using Publisher.Domain.Promotions.Events;
using Publisher.Domain.Registries;

namespace Publisher.Application.Features.Promotions;

/// <summary>
/// Executes a requested promotion: resolves the registry credential and runs the registry copy,
/// then settles the <see cref="Promotion"/>. Discovered by Wolverine as a handler for the
/// <see cref="PromotionRequested"/> domain event, so it runs asynchronously off the request that
/// created the promotion and benefits from the bus's retry/error handling and SQL outbox.
/// </summary>
public sealed class PromotionExecutor
{
    public async Task Handle(
        PromotionRequested evt,
        IPromotionRepository promotions,
        IRemoteRegistryRepository registries,
        ISecretResolver secrets,
        IRegistryPusher pusher,
        IUnitOfWork uow,
        TimeProvider clock,
        ILogger<PromotionExecutor> logger,
        CancellationToken cancellationToken)
    {
        var promotion = await promotions.GetByIdAsync(evt.PromotionId, cancellationToken).ConfigureAwait(false);
        if (promotion is null || promotion.Status != PromotionStatus.Pending) return;

        try
        {
            var registry = await registries.GetByIdAsync(promotion.RegistryId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Registry {promotion.RegistryId} no longer exists.");

            var credential = await BuildCredentialAsync(registry, secrets, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("[promote] Copying {Source} -> {Remote} (promotion {Id}).",
                promotion.SourceRef, promotion.RemoteRef, promotion.Id);

            await pusher.PushAsync(promotion.SourceRef, promotion.RemoteRef, credential, cancellationToken).ConfigureAwait(false);

            promotion.Succeed(clock.GetUtcNow());
            logger.LogInformation("[promote] Succeeded promotion {Id} -> {Remote}.", promotion.Id, promotion.RemoteRef);
        }
        catch (Exception ex)
        {
            promotion.Fail(ex.Message, clock.GetUtcNow());
            logger.LogError(ex, "[promote] Failed promotion {Id} -> {Remote}.", promotion.Id, promotion.RemoteRef);
        }

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<RegistryCredential> BuildCredentialAsync(
        RemoteRegistry registry, ISecretResolver secrets, CancellationToken ct)
    {
        string? secret = null;
        if (registry.CredentialSecretRef is { } secretRef)
            secret = await secrets.ResolveAsync(secretRef, ct).ConfigureAwait(false);

        return new RegistryCredential(registry.AuthMethod, registry.Username, secret);
    }
}
