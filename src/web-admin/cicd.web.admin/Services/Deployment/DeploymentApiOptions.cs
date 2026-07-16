namespace Cicd.Web.Admin.Services.Deployment;

/// <summary>Options for the Deployment.Api typed HttpClient. Bound from <c>"Deployment:Api"</c>.</summary>
public sealed class DeploymentApiOptions
{
    public const string SectionName = "Deployment:Api";
    // Fallback matches the deployment-api's pinned http endpoint (AppHost) so the admin UI still reaches it
    // if the Aspire-injected Deployment:Api:BaseUrl is ever absent.
    public string BaseUrl { get; set; } = "http://localhost:7228";
}
