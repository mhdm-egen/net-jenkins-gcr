using Deployment.Contracts.Catalog;
using DeployableApplication = Deployment.Domain.DeployableUnits.Application;

namespace Deployment.Application.Features.Catalog.Applications;

internal static class ApplicationMapping
{
    /// <summary>
    /// Projects the aggregate + a parallel "service id → name" lookup into the
    /// wire DTO. The lookup is supplied by the caller so the mapper stays pure
    /// (no repository roundtrip per service member).
    /// </summary>
    public static ApplicationDto ToDto(this DeployableApplication app, IReadOnlyDictionary<Guid, string> serviceNames)
    {
        var members = app.Services
            .Select(m => new ApplicationServiceMemberDto(
                ServiceId: m.ServiceId,
                ServiceName: serviceNames.TryGetValue(m.ServiceId, out var n) ? n : "(unknown)",
                Role: m.Role,
                IsOptional: m.IsOptional,
                DeploymentOrder: m.DeploymentOrder))
            .ToList();

        return new ApplicationDto(
            Id: app.Id,
            Name: app.Name,
            Description: app.Description,
            IsActive: app.IsActive,
            CreatedAtUtc: app.CreatedAtUtc,
            Services: members);
    }
}
