namespace Deployment.Application.Features.Deployments.GetDeploymentBaseline;

public sealed class GetDeploymentBaselineHandler
{
    private readonly IDeploymentBaselineReader _reader;

    public GetDeploymentBaselineHandler(IDeploymentBaselineReader reader)
    {
        _reader = reader;
    }

    public Task<DeploymentBaseline?> HandleAsync(
        GetDeploymentBaselineQuery query,
        CancellationToken cancellationToken = default)
        => _reader.ReadAsync(query.DeploymentId, cancellationToken);
}
