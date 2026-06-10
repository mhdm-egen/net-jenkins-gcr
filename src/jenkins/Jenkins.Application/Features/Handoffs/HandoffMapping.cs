using Jenkins.Contracts.Handoffs;
using Jenkins.Domain.Handoffs;

namespace Jenkins.Application.Features.Handoffs;

internal static class HandoffMapping
{
    public static ContainerReleaseHandoffDto ToDto(this ContainerReleaseHandoff h) => new(
        Id: h.Id,
        BuildId: h.BuildId,
        BuildArtifactId: h.BuildArtifactId,
        DeployableComponentId: h.DeployableComponentId,
        RepositoryId: h.RepositoryId,
        DeployableUnitId: h.DeployableUnitId,
        DeploymentReleaseId: h.DeploymentReleaseId,
        SemanticVersion: h.SemanticVersion,
        ArtifactUri: h.ArtifactUri,
        Status: (HandoffStatusDto)(int)h.Status,
        RequestedByPrincipal: h.RequestedByPrincipal,
        CreatedAtUtc: h.CreatedAtUtc,
        SettledAtUtc: h.SettledAtUtc,
        FailureReason: h.FailureReason);
}
