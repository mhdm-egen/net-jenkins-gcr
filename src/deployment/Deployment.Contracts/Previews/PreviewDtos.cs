namespace Deployment.Contracts.Previews;

public enum PreviewStatusDto { Creating = 0, Active = 1, Failed = 2, TornDown = 3 }

public sealed record PreviewEnvironmentDto(
    Guid Id,
    Guid ApplicationId,
    string ApplicationName,
    string Key,
    string KubeContext,
    string Namespace,
    string ManifestSource,
    string? Version,
    PreviewStatusDto Status,
    string TriggeredBy,
    string? Log,
    string? FailureReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? ActivatedAtUtc,
    DateTimeOffset? TornDownAtUtc);

/// <summary>Stand up a preview environment for an Aspire app. <see cref="ManifestSource"/>/<see cref="Version"/>
/// default to the app's current when omitted (e.g. pass a branch build's manifest URL to preview that build).</summary>
public sealed record CreatePreviewEnvironmentRequest(
    Guid ApplicationId,
    string Key,
    string? ManifestSource = null,
    string? Version = null,
    int? TtlHours = null,
    string? TriggeredBy = null);

/// <summary>Normalized preview-lifecycle webhook a git provider (or Jenkins) posts on PR events. A close/merge
/// action tears down the preview matching the app + key (branch / PR). Identify the app by <see cref="AppName"/>
/// (its CI source key or name) or <see cref="ApplicationId"/>.</summary>
public sealed record PreviewWebhookRequest(
    string? AppName,
    Guid? ApplicationId,
    string Key,
    string? Action = null);
