namespace Jenkins.Infrastructure.Sync;

/// <summary>
/// Options for <see cref="JenkinsBuildSyncService"/>. Bound from configuration
/// section <c>"Jenkins:Sync"</c>.
/// </summary>
public sealed class JenkinsSyncOptions
{
    public const string SectionName = "Jenkins:Sync";

    /// <summary>Master switch for the background sync (host still runs when off).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Delay between sync ticks.</summary>
    public int PollIntervalSeconds { get; set; } = 30;

    /// <summary>How many recent builds to pull on a job's first sync (catch-up).</summary>
    public int BackfillCount { get; set; } = 25;

    /// <summary>How many recent builds to pull on subsequent ticks.</summary>
    public int PerJobFetchCount { get; set; } = 10;
}
