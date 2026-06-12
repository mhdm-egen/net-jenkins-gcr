namespace Publisher.Contracts.Promotions;

public enum PromotionStatusDto
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Skipped = 3,
}

public sealed record PromotionDto(
    Guid Id,
    Guid ContainerId,
    Guid RegistryId,
    string RegistryName,
    Guid? RuleId,
    string TriggeredBy,
    string SourceRef,
    string RemoteRef,
    Guid RepositoryId,
    string ContainerName,
    string Version,
    PromotionStatusDto Status,
    string? FailureReason,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? CompletedAtUtc);

/// <summary>Body for a manual promotion: which registry to push to (defaults to the default registry).</summary>
public sealed record PromoteContainerRequest(Guid? RegistryId, string? TriggeredBy);
