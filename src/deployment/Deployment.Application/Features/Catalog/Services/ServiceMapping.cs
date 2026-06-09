using Deployment.Contracts.Catalog;
using Deployment.Domain.DeployableUnits;

namespace Deployment.Application.Features.Catalog.Services;

/// <summary>
/// Single place that converts between the domain <see cref="Service"/> and the
/// wire-format <see cref="ServiceDto"/>. Keeping all the projection in one
/// place lets us spot drift fast when the domain shape evolves.
/// </summary>
internal static class ServiceMapping
{
    public static ServiceDto ToDto(this Service s) => new(
        Id: s.Id,
        Name: s.Name,
        Kind: (ServiceKindDto)(int)s.Kind,
        RepositoryUrl: s.RepositoryUrl,
        TargetFramework: s.TargetFramework,
        IsActive: s.IsActive,
        CreatedAtUtc: s.CreatedAtUtc);

    public static ServiceKind ToDomain(this ServiceKindDto k) => (ServiceKind)(int)k;
}
