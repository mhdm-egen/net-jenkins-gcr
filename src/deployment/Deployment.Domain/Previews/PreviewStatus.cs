namespace Deployment.Domain.Previews;

/// <summary>Lifecycle of a preview environment.</summary>
public enum PreviewStatus
{
    /// <summary>Requested; the executor is applying the manifest to the preview namespace.</summary>
    Creating = 0,

    /// <summary>Deployed and running in its namespace.</summary>
    Active = 1,

    /// <summary>The deploy failed.</summary>
    Failed = 2,

    /// <summary>The namespace has been deleted (manual teardown or TTL sweep). Terminal.</summary>
    TornDown = 3,
}
