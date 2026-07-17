namespace Jenkins.Client;

public interface IJenkinsClient
{
    // --- Connectivity ---

    /// <summary>
    /// Hits a lightweight Jenkins endpoint to confirm reachability + credentials.
    /// Throws <see cref="HttpRequestException"/> with a populated <see cref="HttpRequestException.StatusCode"/>
    /// on HTTP errors; throws other exceptions on network / parse / timeout failures.
    /// </summary>
    Task PingAsync(CancellationToken cancellationToken = default);

    // --- Discovery ---

    /// <summary>
    /// Lists top-level jobs visible to the configured user. Returns a shallow
    /// view (name, url, color, last-build-number); fetch <see cref="GetJobDetailsAsync"/>
    /// for parameter definitions.
    /// </summary>
    Task<IReadOnlyList<JenkinsJobSummary>> ListJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the job's description and parameter definitions. Parameter list
    /// is empty for non-parameterized jobs.
    /// </summary>
    Task<JenkinsJobDetails> GetJobDetailsAsync(string jobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent builds of <paramref name="jobName"/>, newest first.
    /// Capped at <paramref name="count"/> (Jenkins's <c>tree</c> range syntax handles
    /// pagination so the request stays a single round-trip). Each entry includes
    /// status, timestamps, duration, and the build's description (typically the
    /// PACKAGE_VERSION for our pipelines).
    /// </summary>
    Task<IReadOnlyList<Build>> ListBuildsAsync(string jobName, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full record for a single build — basics plus the artifacts list
    /// and the causes that triggered the run. One round-trip via a tree query.
    /// </summary>
    Task<JenkinsBuildDetails> GetBuildDetailsAsync(string jobName, int buildNumber, CancellationToken cancellationToken = default);

    // --- Triggering ---

    /// <summary>
    /// Triggers a new build of <paramref name="jobName"/>. Returns the queue item id;
    /// use <see cref="WaitForBuildToStartAsync"/> to resolve it to a build number.
    /// </summary>
    Task<long> StartBuildAsync(
        string jobName,
        IDictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default);

    // --- Queue tracking ---

    Task<QueueItem> GetQueueItemAsync(long queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls the queue until the item has been assigned an executor (or cancelled).
    /// Returns the resulting build number.
    /// </summary>
    Task<int> WaitForBuildToStartAsync(
        long queueId,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    // --- Build tracking ---

    Task<Build> GetBuildAsync(string jobName, int buildNumber, CancellationToken cancellationToken = default);

    Task<Build> GetLastBuildAsync(string jobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls the build until <c>Building == false</c>. Returns the final build with its result.
    /// </summary>
    Task<Build> WaitForBuildToFinishAsync(
        string jobName,
        int buildNumber,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    Task StopBuildAsync(string jobName, int buildNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a build record on the Jenkins server (<c>POST .../{n}/doDelete</c>). Idempotent —
    /// a missing build (404) is treated as success. Requires the token to have Run/Delete permission.
    /// </summary>
    Task DeleteBuildAsync(string jobName, int buildNumber, CancellationToken cancellationToken = default);

    // --- Outputs ---

    Task<string> GetConsoleLogAsync(string jobName, int buildNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the live console log for a build via Jenkins's <c>progressiveHtml</c>
    /// endpoint. Polls every <paramref name="pollInterval"/> (default 1s), yielding the
    /// body of each response as a chunk. Returns when Jenkins reports the build's log
    /// is complete (no <c>X-More-Data</c> header) or cancellation is requested.
    /// Output is HTML pre-rendered by Jenkins (ANSI colors, timestamps, decorators).
    /// </summary>
    IAsyncEnumerable<string> StreamConsoleLogAsync(
        string jobName,
        int buildNumber,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single archived artifact (relative path under the build's artifacts/) as raw bytes.
    /// Useful for pulling build-info.json or similar manifests in an orchestrator.
    /// </summary>
    Task<byte[]> GetArtifactAsync(
        string jobName,
        int buildNumber,
        string artifactPath,
        CancellationToken cancellationToken = default);

    // --- Convenience: trigger + wait + return final result ---

    /// <summary>
    /// Triggers a build and waits for it to both start and finish. Returns the completed build.
    /// </summary>
    Task<Build> RunBuildAsync(
        string jobName,
        IDictionary<string, string>? parameters = null,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
