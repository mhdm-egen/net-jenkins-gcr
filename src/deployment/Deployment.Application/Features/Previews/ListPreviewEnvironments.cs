using Deployment.Contracts.Previews;

namespace Deployment.Application.Features.Previews;

public interface IPreviewEnvironmentReader
{
    Task<IReadOnlyList<PreviewEnvironmentDto>> ListAsync(Guid? applicationId = null, bool includeTornDown = false, CancellationToken ct = default);
    Task<PreviewEnvironmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

public sealed record ListPreviewEnvironmentsQuery(Guid? ApplicationId, bool IncludeTornDown);
public sealed class ListPreviewEnvironmentsHandler
{
    private readonly IPreviewEnvironmentReader _reader;
    public ListPreviewEnvironmentsHandler(IPreviewEnvironmentReader reader) => _reader = reader;
    public Task<IReadOnlyList<PreviewEnvironmentDto>> HandleAsync(ListPreviewEnvironmentsQuery q, CancellationToken ct = default)
        => _reader.ListAsync(q.ApplicationId, q.IncludeTornDown, ct);
}

public sealed record GetPreviewEnvironmentByIdQuery(Guid Id);
public sealed class GetPreviewEnvironmentByIdHandler
{
    private readonly IPreviewEnvironmentReader _reader;
    public GetPreviewEnvironmentByIdHandler(IPreviewEnvironmentReader reader) => _reader = reader;
    public Task<PreviewEnvironmentDto?> HandleAsync(GetPreviewEnvironmentByIdQuery q, CancellationToken ct = default)
        => _reader.GetByIdAsync(q.Id, ct);
}
