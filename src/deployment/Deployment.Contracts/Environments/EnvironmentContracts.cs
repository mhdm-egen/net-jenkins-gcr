namespace Deployment.Contracts.Environments;

// Wire-stable mirror of Deployment.Domain.Environments.TargetKind.
// Integer values must match the domain enum one-for-one.
public enum TargetKindDto
{
    IIS = 0,
    AzureAppService = 1,
    KubernetesCluster = 2,
    /// <summary>Azure Container Apps.</summary>
    ContainerApp = 3,
    VM = 4,
    GoogleCloudRun = 5,
}

// --- Read-side DTOs ---

public sealed record EnvironmentDto(
    Guid Id,
    string Name,
    int PromotionRank,
    bool RequiresApproval,
    bool IsProduction,
    IReadOnlyList<DeploymentTargetDto> Targets,
    IReadOnlyList<EnvironmentFreezeWindowDto> FreezeWindows);

public sealed record DeploymentTargetDto(
    Guid Id,
    Guid EnvironmentId,
    TargetKindDto TargetKind,
    string ResourceId,
    string Region,
    string? Slot);

public sealed record EnvironmentFreezeWindowDto(
    Guid Id,
    Guid EnvironmentId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Reason,
    string CreatedByPrincipal,
    DateTimeOffset CreatedAtUtc);

// --- Write-side requests ---

public sealed record RegisterEnvironmentRequest(
    string Name,
    int PromotionRank,
    bool RequiresApproval,
    bool IsProduction);

public sealed record RenameEnvironmentRequest(string Name);

public sealed record ChangePromotionRankRequest(int PromotionRank);

public sealed record SetApprovalRequirementRequest(bool RequiresApproval);

public sealed record SetProductionFlagRequest(bool IsProduction);

public sealed record AddTargetRequest(
    TargetKindDto TargetKind,
    string ResourceId,
    string Region,
    string? Slot);

public sealed record UpdateTargetRequest(
    TargetKindDto TargetKind,
    string ResourceId,
    string Region,
    string? Slot);

public sealed record ScheduleFreezeWindowRequest(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Reason,
    string CreatedByPrincipal);
