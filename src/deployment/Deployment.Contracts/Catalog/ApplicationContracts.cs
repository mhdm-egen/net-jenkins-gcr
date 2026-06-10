namespace Deployment.Contracts.Catalog;

public sealed record ApplicationDto(
    Guid Id,
    string Name,
    string Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ApplicationServiceMemberDto> Services);

/// <summary>
/// A member of an Application's catalog. Wire-side mirror of the domain
/// <c>ApplicationService</c> child entity (composite identity AppId+ServiceId).
/// <see cref="ServiceName"/> is denormalized server-side for UI convenience —
/// it's not part of the domain entity.
/// </summary>
public sealed record ApplicationServiceMemberDto(
    Guid ServiceId,
    string ServiceName,
    string Role,
    bool IsOptional,
    int DeploymentOrder);

public sealed record RegisterApplicationRequest(
    string Name,
    string Description);

public sealed record RenameApplicationRequest(string Name);

public sealed record ChangeApplicationDescriptionRequest(string Description);

public sealed record AddApplicationMemberRequest(
    Guid ServiceId,
    string Role,
    bool IsOptional,
    int DeploymentOrder);

public sealed record UpdateApplicationMemberRequest(
    string Role,
    bool IsOptional,
    int DeploymentOrder);
