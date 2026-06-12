using Microsoft.Extensions.Logging;
using Publisher.Domain.Abstractions;
using Publisher.Domain.Containers;
using Publisher.Domain.Promotions;
using Publisher.Domain.Registries;

namespace Publisher.Application.Features.Promotions;

/// <summary>
/// Requests a push of one inventory container to one remote registry. Shared by the manual API
/// endpoint and the rule-driven path. Creates a <see cref="Promotion"/> (Pending) whose
/// <c>PromotionRequested</c> domain event drives <see cref="PromotionExecutor"/>. Idempotent:
/// skips if an active/succeeded promotion of the same container→registry already exists, or if
/// the registry is missing/disabled.
/// </summary>
public sealed record PromoteContainerCommand(
    Guid ContainerId,
    Guid RegistryId,
    Guid? RuleId,
    string? TriggeredBy);

public sealed record PromoteResult(Guid? PromotionId, string Outcome);

public sealed class PromoteContainerHandler
{
    private readonly IPublishableContainerRepository _containers;
    private readonly IRemoteRegistryRepository _registries;
    private readonly IPromotionRepository _promotions;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    private readonly ILogger<PromoteContainerHandler> _logger;

    public PromoteContainerHandler(
        IPublishableContainerRepository containers,
        IRemoteRegistryRepository registries,
        IPromotionRepository promotions,
        IUnitOfWork uow,
        TimeProvider clock,
        ILogger<PromoteContainerHandler> logger)
    {
        _containers = containers;
        _registries = registries;
        _promotions = promotions;
        _uow = uow;
        _clock = clock;
        _logger = logger;
    }

    public async Task<PromoteResult> HandleAsync(PromoteContainerCommand cmd, CancellationToken cancellationToken = default)
    {
        var container = await _containers.GetByIdAsync(cmd.ContainerId, cancellationToken).ConfigureAwait(false);
        if (container is null) return new PromoteResult(null, "container-not-found");
        if (!container.IsActive) return new PromoteResult(null, "container-inactive");

        var registry = await _registries.GetByIdAsync(cmd.RegistryId, cancellationToken).ConfigureAwait(false);
        if (registry is null) return new PromoteResult(null, "registry-not-found");
        if (!registry.Enabled) return new PromoteResult(null, "registry-disabled");

        if (await _promotions.ExistsActiveAsync(container.Id, registry.Id, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation(
                "[promote] Skipping {Container} -> {Registry}: an active/succeeded promotion already exists.",
                container.ContainerName, registry.Name);
            return new PromoteResult(null, "already-promoted");
        }

        var sourceRef = container.ArtifactUri;
        var remoteRef = RemoteReference.BuildDestination(registry, container.ContainerName, container.Version);

        var promotion = new Promotion(
            id: Guid.NewGuid(),
            containerId: container.Id,
            registryId: registry.Id,
            registryName: registry.Name,
            ruleId: cmd.RuleId,
            triggeredBy: cmd.TriggeredBy ?? "system",
            sourceRef: sourceRef,
            remoteRef: remoteRef,
            repositoryId: container.RepositoryId,
            containerName: container.ContainerName,
            version: container.Version,
            requestedAtUtc: _clock.GetUtcNow());

        await _promotions.AddAsync(promotion, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[promote] Requested {Container} -> {Remote} (promotion {Id}).",
            container.ContainerName, remoteRef, promotion.Id);
        return new PromoteResult(promotion.Id, "requested");
    }
}
