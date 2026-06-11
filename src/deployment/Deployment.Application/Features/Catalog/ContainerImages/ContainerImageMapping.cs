using Deployment.Contracts.Catalog;
using Deployment.Domain.ContainerImages;

namespace Deployment.Application.Features.Catalog.ContainerImages;

internal static class ContainerImageMapping
{
    public static ContainerImageDto ToDto(this ContainerImage c) => new(
        Id: c.Id,
        Registry: c.Registry,
        Repository: c.Repository,
        Name: c.Name,
        DefaultTag: c.DefaultTag,
        BaseRef: c.BaseRef,
        IsActive: c.IsActive,
        CreatedAtUtc: c.CreatedAtUtc);
}
