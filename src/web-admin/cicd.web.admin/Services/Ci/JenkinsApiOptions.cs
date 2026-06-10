namespace Cicd.Web.Admin.Services.Ci;

/// <summary>
/// Options for the Jenkins CI service (Jenkins.Api) typed HttpClient. Bound from
/// configuration section <c>"JenkinsApi"</c>. Distinct from the existing
/// <c>"Jenkins"</c> section, which configures the direct Jenkins-server connection.
/// </summary>
public sealed class JenkinsApiOptions
{
    public const string SectionName = "JenkinsApi";

    public string BaseUrl { get; set; } = "http://localhost:5310";
}
