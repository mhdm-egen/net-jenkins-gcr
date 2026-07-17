namespace Deployment.Contracts.Seed;

/// <summary>
/// Admin "demo setup" seed: installs curated demo <b>configuration</b> (environments, services,
/// mappings, Aspire apps) so an operator can then trigger real builds/deploys. One bool per scenario;
/// the handler creates only the selected scenarios' slice, idempotently (find-by-name skip). It never
/// creates runs/previews and raises no deploy — config creation has no executor side-effects.
/// </summary>
public sealed record SeedDemoRequest(bool AspireAutoDeploy, bool BlueGreenK8s, bool CloudRun, bool K8sAdmin);

/// <summary>One seeded item and what happened to it: <c>created</c> or <c>skipped</c> (already present).</summary>
public sealed record SeedItemDto(string Kind, string Name, string Status);

/// <summary>
/// Result of a deployment demo-seed. <see cref="Items"/>/<see cref="Log"/> mirror the reset endpoint's
/// per-item reporting. The Cloud Run service coordinates are echoed back so the caller can thread them
/// into the CI seed (the Jenkins container→component mapping points at this deployment Service id).
/// </summary>
public sealed record SeedDemoResultDto(
    int Created,
    int Skipped,
    IReadOnlyList<SeedItemDto> Items,
    IReadOnlyList<string> Log,
    Guid? CloudRunServiceId,
    string? CloudRunServiceName,
    string? CloudRunContainerName);
