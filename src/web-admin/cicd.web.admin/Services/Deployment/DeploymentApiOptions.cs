namespace Cicd.Web.Admin.Services.Deployment;

/// <summary>
/// What the deployment service says will happen to a manually-triggered digest. Carries the
/// suppression cases explicitly because <c>INotificationDispatcher</c> is fire-and-forget — without
/// this, "queued" and "generated then silently dropped" look identical to the caller.
/// </summary>
/// <param name="Channels">Channels that are enabled AND configured well enough to deliver.</param>
/// <param name="Suppressed">True when OnlyFailures is set, which drops an Info-severity digest.</param>
public sealed record DigestTriggerResult(
    bool Queued,
    IReadOnlyList<string> Channels,
    bool Suppressed,
    string? Note);

/// <summary>Options for the Deployment.Api typed HttpClient. Bound from <c>"Deployment:Api"</c>.</summary>
public sealed class DeploymentApiOptions
{
    public const string SectionName = "Deployment:Api";
    // Fallback matches the deployment-api's pinned http endpoint (AppHost) so the admin UI still reaches it
    // if the Aspire-injected Deployment:Api:BaseUrl is ever absent.
    public string BaseUrl { get; set; } = "http://localhost:7228";
}
