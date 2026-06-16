using Deployment.Domain.Mappings;

namespace Deployment.Application.Abstractions;

/// <summary>
/// Mutable per-run context threaded through the recipe's steps. Steps read the inputs and set the
/// outputs (GarPush sets <see cref="RemoteImageRef"/>, CloudRunDeploy sets <see cref="CloudRunRevision"/>).
/// </summary>
public sealed class DeploymentContext
{
    public required string ContainerName { get; init; }
    public required string Version { get; init; }
    public required string SourceRef { get; init; }       // Nexus pull ref (digest-pinned when available)
    public required string GcpProject { get; init; }
    public required string Region { get; init; }
    public required string GarRepository { get; init; }
    public required string CloudRunServiceName { get; init; }

    public string? RemoteImageRef { get; set; }            // set by GarPush
    public string? CloudRunRevision { get; set; }          // set by CloudRunDeploy

    /// <summary>The image a deploy step should run: the promoted GAR ref if present, else the source.</summary>
    public string ImageToDeploy => string.IsNullOrWhiteSpace(RemoteImageRef) ? SourceRef : RemoteImageRef!;
}

public sealed record StepOutcome(bool Success, string? Detail)
{
    public static StepOutcome Ok(string? detail = null) => new(true, detail);
    public static StepOutcome Fail(string detail) => new(false, detail);
}

/// <summary>One handler per <see cref="DeploymentStepKind"/>. The run executor dispatches by Kind.</summary>
public interface IDeploymentStepExecutor
{
    DeploymentStepKind Kind { get; }
    Task<StepOutcome> ExecuteAsync(DeploymentContext context, CancellationToken cancellationToken = default);
}
