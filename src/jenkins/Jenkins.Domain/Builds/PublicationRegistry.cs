namespace Jenkins.Domain.Builds;

/// <summary>
/// Registry an artifact was pushed to. Deliberately Nexus-only: GAR is owned by
/// the deployment service (CI decision #6), so it is not a value here.
/// </summary>
public enum PublicationRegistry
{
    NexusNuGet = 0,
    NexusDocker = 1,
}
