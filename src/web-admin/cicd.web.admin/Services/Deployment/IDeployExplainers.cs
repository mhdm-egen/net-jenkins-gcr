namespace Cicd.Web.Admin.Services.Deployment;

/// <summary>
/// Explains why a per-service deployment run failed, grounded in its typed per-step record.
/// Soft-fails: <see cref="IsConfigured"/> is false with no AI key and the UI hides the action.
/// </summary>
public interface IDeployRunExplainer
{
    bool IsConfigured { get; }

    Task<DeployExplanation> ExplainAsync(DeployRunExplainRequest request, CancellationToken ct = default);
}

/// <summary>
/// Explains a whole-Aspire-app deploy from its aspirate log — the failure on a failed run, or the
/// warnings that explain an unreachable app on a succeeded one. Soft-fails like the above.
/// </summary>
public interface IAspireRunExplainer
{
    bool IsConfigured { get; }

    Task<DeployExplanation> ExplainAsync(AspireRunExplainRequest request, CancellationToken ct = default);
}
