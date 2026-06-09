using Deployment.Contracts.Deployments;

namespace Deployment.Application.Features.Deployments.ListDeployments;

/// <summary>
/// Read-model port for the Deployments UI. Backed by an EF projection
/// in Infrastructure; the Application layer stays persistence-agnostic.
/// </summary>
public interface IDeploymentCatalogReader
{
    /// <summary>
    /// List deployments with optional filters. <c>OnlyParents</c> = true hides
    /// child rows from cascade fan-outs so the list reads as one row per
    /// logical deployment event.
    /// </summary>
    Task<IReadOnlyList<DeploymentSummaryDto>> ListAsync(
        Guid? environmentId,
        DeploymentStatusDto? status,
        Guid? releaseId,
        bool onlyParents,
        int take,
        CancellationToken cancellationToken = default);

    Task<DeploymentDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ListDeploymentsQuery(
    Guid? EnvironmentId,
    DeploymentStatusDto? Status,
    Guid? ReleaseId,
    bool OnlyParents,
    int Take);

public sealed class ListDeploymentsHandler
{
    private readonly IDeploymentCatalogReader _reader;
    public ListDeploymentsHandler(IDeploymentCatalogReader reader) => _reader = reader;

    public Task<IReadOnlyList<DeploymentSummaryDto>> HandleAsync(
        ListDeploymentsQuery query, CancellationToken cancellationToken = default)
        => _reader.ListAsync(query.EnvironmentId, query.Status, query.ReleaseId,
            query.OnlyParents, query.Take, cancellationToken);
}

public sealed record GetDeploymentByIdQuery(Guid Id);

public sealed class GetDeploymentByIdHandler
{
    private readonly IDeploymentCatalogReader _reader;
    public GetDeploymentByIdHandler(IDeploymentCatalogReader reader) => _reader = reader;

    public Task<DeploymentDetailDto?> HandleAsync(GetDeploymentByIdQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByIdAsync(query.Id, cancellationToken);
}
