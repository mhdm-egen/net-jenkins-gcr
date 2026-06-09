namespace Deployment.Application.Features.Deployments.GetEffectiveVersions;

/// <summary>
/// Read-model port for Q1′ (decisions §10.4). Backed by an EF/SQL projection
/// in Infrastructure; kept out of Application so the handler stays
/// persistence-agnostic.
/// </summary>
public interface IEffectiveVersionsReader
{
    Task<IReadOnlyList<EffectiveVersionEntry>> ReadAsync(
        Guid applicationId,
        Guid environmentId,
        CancellationToken cancellationToken = default);
}
