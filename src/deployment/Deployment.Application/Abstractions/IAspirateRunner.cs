namespace Deployment.Application.Abstractions;

/// <summary>Inputs for an Aspir8 deploy of a registered Aspire application.</summary>
public sealed record AspirateDeployRequest(string AppHostPath, string KubeContext, string Namespace);

/// <summary>Outcome of the aspirate shell-out: success + the combined CLI log (+ a reason on failure).</summary>
public sealed record AspirateDeployResult(bool Success, string Log, string? FailureReason);

/// <summary>
/// Port over the Aspir8 (<c>aspirate</c>) CLI: <c>generate --skip-build</c> (manifests from the
/// already-pushed Nexus images) then <c>apply</c> to the target Kubernetes context. Implemented in
/// Infrastructure as a process shell-out (mirrors the crane promoter). The handler depends only on
/// this port.
/// </summary>
public interface IAspirateRunner
{
    Task<AspirateDeployResult> DeployAsync(AspirateDeployRequest request, CancellationToken cancellationToken = default);
}
