using Deployment.Contracts.AspireApps;

namespace Deployment.Application.Features.AspireApps;

public interface IAspireApplicationReader
{
    Task<IReadOnlyList<AspireApplicationDto>> ListAsync(CancellationToken ct = default);
    Task<AspireApplicationDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

public sealed record ListAspireApplicationsQuery;
public sealed class ListAspireApplicationsHandler
{
    private readonly IAspireApplicationReader _reader;
    public ListAspireApplicationsHandler(IAspireApplicationReader reader) => _reader = reader;
    public Task<IReadOnlyList<AspireApplicationDto>> HandleAsync(ListAspireApplicationsQuery q, CancellationToken ct = default) => _reader.ListAsync(ct);
}

public sealed record GetAspireApplicationByIdQuery(Guid Id);
public sealed class GetAspireApplicationByIdHandler
{
    private readonly IAspireApplicationReader _reader;
    public GetAspireApplicationByIdHandler(IAspireApplicationReader reader) => _reader = reader;
    public Task<AspireApplicationDto?> HandleAsync(GetAspireApplicationByIdQuery q, CancellationToken ct = default) => _reader.GetByIdAsync(q.Id, ct);
}

public interface IAspireApplicationRunReader
{
    Task<IReadOnlyList<AspireApplicationRunDto>> ListAsync(Guid? applicationId = null, CancellationToken ct = default);
    Task<AspireApplicationRunDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

public sealed record ListAspireRunsQuery(Guid? ApplicationId);
public sealed class ListAspireRunsHandler
{
    private readonly IAspireApplicationRunReader _reader;
    public ListAspireRunsHandler(IAspireApplicationRunReader reader) => _reader = reader;
    public Task<IReadOnlyList<AspireApplicationRunDto>> HandleAsync(ListAspireRunsQuery q, CancellationToken ct = default) => _reader.ListAsync(q.ApplicationId, ct);
}

public sealed record GetAspireRunByIdQuery(Guid Id);
public sealed class GetAspireRunByIdHandler
{
    private readonly IAspireApplicationRunReader _reader;
    public GetAspireRunByIdHandler(IAspireApplicationRunReader reader) => _reader = reader;
    public Task<AspireApplicationRunDto?> HandleAsync(GetAspireRunByIdQuery q, CancellationToken ct = default) => _reader.GetByIdAsync(q.Id, ct);
}
