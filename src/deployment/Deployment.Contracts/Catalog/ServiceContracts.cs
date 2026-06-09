namespace Deployment.Contracts.Catalog;

/// <summary>Service kinds mirrored from the domain enum. Wire-stable integer values.</summary>
public enum ServiceKindDto
{
    WebApi = 0,
    Mvc = 1,
    WorkerService = 2,
    AzureFunction = 3,
    Console = 4,
    Other = 5,
}

public sealed record ServiceDto(
    Guid Id,
    string Name,
    ServiceKindDto Kind,
    string RepositoryUrl,
    string TargetFramework,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public sealed record RegisterServiceRequest(
    string Name,
    ServiceKindDto Kind,
    string RepositoryUrl,
    string TargetFramework);

public sealed record RenameServiceRequest(string Name);

public sealed record UpdateServiceRepositoryInfoRequest(
    string RepositoryUrl,
    string TargetFramework);
