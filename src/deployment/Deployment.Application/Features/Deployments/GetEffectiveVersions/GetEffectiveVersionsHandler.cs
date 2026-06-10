namespace Deployment.Application.Features.Deployments.GetEffectiveVersions;

public sealed class GetEffectiveVersionsHandler
{
    private readonly IEffectiveVersionsReader _reader;

    public GetEffectiveVersionsHandler(IEffectiveVersionsReader reader)
    {
        _reader = reader;
    }

    public async Task<EffectiveVersions> HandleAsync(
        GetEffectiveVersionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var rows = await _reader.ReadAsync(query.ApplicationId, query.EnvironmentId, cancellationToken)
            .ConfigureAwait(false);
        return new EffectiveVersions(rows);
    }
}
