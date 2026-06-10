namespace Jenkins.Domain.Builds;

/// <summary>Outcome of pushing an artifact to a registry.</summary>
public enum PublicationStatus
{
    Pushed = 0,
    Failed = 1,
}
