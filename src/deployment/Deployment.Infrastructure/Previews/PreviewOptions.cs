namespace Deployment.Infrastructure.Previews;

/// <summary>Options for preview environments. Bound from <c>Deployment:Previews</c>.</summary>
public sealed class PreviewOptions
{
    public const string SectionName = "Deployment:Previews";

    /// <summary>How often the TTL sweeper checks for expired previews to tear down. Default 15 minutes.</summary>
    public int SweepIntervalMinutes { get; set; } = 15;
}
