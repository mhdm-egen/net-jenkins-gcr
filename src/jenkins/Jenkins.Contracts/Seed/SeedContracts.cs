namespace Jenkins.Contracts.Seed;

/// <summary>
/// Admin "demo setup" seed for CI: registers the demo tracked repositories (and, for the Cloud Run
/// scenario, the container→component mapping) so an operator can trigger real pipeline runs. Additive
/// and idempotent (find-by-name skip). Pipelines "CICD Main" / "Aspire build" are already auto-seeded
/// at startup, so this only ensures repositories. The <c>DeployableUnit*</c> fields carry the
/// deployment-side Cloud Run Service coordinates (from the deployment seed) for the component mapping.
/// </summary>
public sealed record SeedDemoCiRequest(
    bool AspireRepo,
    bool CloudRunRepo,
    Guid? DeployableUnitId,
    string? DeployableUnitName,
    string? ContainerName);

/// <summary>One seeded item and what happened to it: <c>created</c> or <c>skipped</c> (already present).</summary>
public sealed record SeedCiItemDto(string Kind, string Name, string Status);

/// <summary>Result of a CI demo-seed — per-item reporting mirroring the reset endpoint's shape.</summary>
public sealed record SeedCiResultDto(int Created, int Skipped, IReadOnlyList<SeedCiItemDto> Items, IReadOnlyList<string> Log);
