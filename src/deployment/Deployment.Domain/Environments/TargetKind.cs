namespace Deployment.Domain.Environments;

/// <summary>
/// Physical/logical host shape a deployment target represents. Drives
/// adapter selection in the deployment runner (not in this model).
/// </summary>
public enum TargetKind
{
    IIS = 0,
    AzureAppService = 1,
    KubernetesCluster = 2,
    /// <summary>Azure Container Apps (managed container platform on Azure).</summary>
    ContainerApp = 3,
    VM = 4,
    /// <summary>
    /// Google Cloud Run — fully-managed container platform on GCP.
    /// ResourceId convention: <c>projects/{project}/locations/{region}/services/{service}</c>.
    /// </summary>
    GoogleCloudRun = 5,
}
