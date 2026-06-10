using Jenkins.Contracts.Handoffs;

namespace Jenkins.Application.Features.Handoffs;

/// <summary>Read-model port for handoffs (flat projections to the wire DTO).</summary>
public interface IHandoffReader
{
    Task<IReadOnlyList<ContainerReleaseHandoffDto>> ListByBuildAsync(Guid buildId, CancellationToken cancellationToken = default);
    Task<ContainerReleaseHandoffDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ListHandoffsByBuildQuery(Guid BuildId);

public sealed class ListHandoffsByBuildHandler
{
    private readonly IHandoffReader _reader;
    public ListHandoffsByBuildHandler(IHandoffReader reader) => _reader = reader;

    public Task<IReadOnlyList<ContainerReleaseHandoffDto>> HandleAsync(ListHandoffsByBuildQuery query, CancellationToken cancellationToken = default)
        => _reader.ListByBuildAsync(query.BuildId, cancellationToken);
}

public sealed record GetHandoffByIdQuery(Guid Id);

public sealed class GetHandoffByIdHandler
{
    private readonly IHandoffReader _reader;
    public GetHandoffByIdHandler(IHandoffReader reader) => _reader = reader;

    public Task<ContainerReleaseHandoffDto?> HandleAsync(GetHandoffByIdQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByIdAsync(query.Id, cancellationToken);
}
