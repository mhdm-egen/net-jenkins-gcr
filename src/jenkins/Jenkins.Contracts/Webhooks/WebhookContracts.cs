namespace Jenkins.Contracts.Webhooks;

/// <summary>
/// Normalized git PR-lifecycle webhook. A thin provider adapter maps GitHub <c>pull_request</c> /
/// GitLab merge-request payloads onto this shape (or a caller posts it directly).
/// <para><see cref="Repository"/> is the <b>registered CI repository name</b> (as shown under CI →
/// Repositories). <see cref="Action"/> drives routing: opened/synchronize/reopened → build the PR
/// branch (preview); closed/merged/deleted → tear the preview down. <see cref="AppName"/> is the
/// deployment app's source key for teardown; when omitted the repository name is used.</para>
/// </summary>
public sealed record GitWebhookRequest(
    string Repository,
    string Branch,
    string Action,
    int? PrNumber = null,
    string? AppName = null);
