using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jenkins.Client;

public sealed class JenkinsClient : IJenkinsClient, IDisposable
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultTimeout      = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private (string Field, string Value)? _crumb;       // null = not yet fetched; (null,null) sentinel handled below
    private bool _crumbFetched;
    private readonly SemaphoreSlim _crumbLock = new(1, 1);

    public JenkinsClient(JenkinsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _http = new HttpClient { BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/") };
        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{options.User}:{options.ApiToken}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        _ownsHttp = true;
    }

    /// <summary>For advanced callers (DI, HttpClientFactory). Caller owns the HttpClient.</summary>
    public JenkinsClient(HttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttp = false;
    }

    // --- Public surface ---

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        // ?tree=mode trims the response to a single field so Jenkins doesn't serialize the
        // whole root metadata blob for what's effectively a heartbeat.
        using var resp = await _http.GetAsync("api/json?tree=mode", cancellationToken);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<JenkinsJobSummary>> ListJobsAsync(CancellationToken cancellationToken = default)
    {
        // ?tree= projects only what we need so the response stays small even on
        // instances with hundreds of jobs.
        var dto = await GetJsonAsync<JobListDto>(
            "api/json?tree=jobs[name,url,color,buildable,lastBuild[number]]",
            cancellationToken);

        if (dto.Jobs is null) return Array.Empty<JenkinsJobSummary>();

        var list = new List<JenkinsJobSummary>(dto.Jobs.Length);
        foreach (var j in dto.Jobs)
        {
            list.Add(new JenkinsJobSummary(
                Name:            j.Name ?? string.Empty,
                Url:             j.Url ?? string.Empty,
                Color:           j.Color,
                Buildable:       j.Buildable ?? true,
                LastBuildNumber: j.LastBuild?.Number));
        }
        return list;
    }

    public async Task<JenkinsJobDetails> GetJobDetailsAsync(string jobName, CancellationToken cancellationToken = default)
    {
        // ParametersDefinitionProperty lives in `property[]`; its `parameterDefinitions[]`
        // is the actual list. Each definition has _class (e.g.
        // hudson.model.StringParameterDefinition), name, description, defaultParameterValue,
        // and (for ChoiceParameterDefinition) choices.
        var path = $"{JobPath(jobName)}/api/json?tree=description,property[parameterDefinitions[name,type,description,defaultParameterValue[value],choices]]";
        var dto = await GetJsonAsync<JobDetailsDto>(path, cancellationToken);

        var parameters = new List<JenkinsParameterDefinition>();
        if (dto.Property is { Length: > 0 } props)
        {
            foreach (var prop in props)
            {
                if (prop.ParameterDefinitions is not { Length: > 0 } defs) continue;
                foreach (var def in defs)
                {
                    parameters.Add(ConvertParameter(def));
                }
            }
        }

        return new JenkinsJobDetails(
            Name:        jobName,
            Description: dto.Description,
            Parameters:  parameters);
    }

    private static JenkinsParameterDefinition ConvertParameter(ParameterDefinitionDto def)
    {
        // The `type` field carries the simple class name (e.g. "StringParameterDefinition",
        // "ChoiceParameterDefinition"). We map by suffix to be tolerant of plugin variants.
        var type = MapType(def.Type);

        string? defaultValue = null;
        if (def.DefaultParameterValue?.Value is JsonElement el)
        {
            defaultValue = el.ValueKind switch
            {
                JsonValueKind.String              => el.GetString(),
                JsonValueKind.True                => "true",
                JsonValueKind.False               => "false",
                JsonValueKind.Number              => el.GetRawText(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _                                 => el.GetRawText()
            };
        }

        return new JenkinsParameterDefinition(
            Name:         def.Name ?? string.Empty,
            Type:         type,
            Description:  def.Description,
            DefaultValue: defaultValue,
            Choices:      def.Choices ?? Array.Empty<string>());
    }

    private static JenkinsParameterType MapType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return JenkinsParameterType.Unknown;
        // Tolerate both bare class names and fully-qualified `hudson.model.*` strings.
        if (type.Contains("Password",  StringComparison.OrdinalIgnoreCase)) return JenkinsParameterType.Password;
        if (type.Contains("Boolean",   StringComparison.OrdinalIgnoreCase)) return JenkinsParameterType.Boolean;
        if (type.Contains("Choice",    StringComparison.OrdinalIgnoreCase)) return JenkinsParameterType.Choice;
        if (type.Contains("Text",      StringComparison.OrdinalIgnoreCase)) return JenkinsParameterType.Text;
        if (type.Contains("String",    StringComparison.OrdinalIgnoreCase)) return JenkinsParameterType.String;
        return JenkinsParameterType.Unknown;
    }

    public async Task<JenkinsBuildDetails> GetBuildDetailsAsync(string jobName, int buildNumber, CancellationToken cancellationToken = default)
    {
        // The actions array is heterogeneous — most entries are parameter / SCM /
        // queue actions; only some carry `causes[]`. We project just the fields we
        // need; non-cause actions deserialize with empty Causes and get filtered out.
        var path = $"{JobPath(jobName)}/{buildNumber}/api/json"
                 + "?tree=number,url,building,result,timestamp,duration,description,"
                 + "artifacts[fileName,relativePath],"
                 + "actions[causes[shortDescription]]";

        var dto = await GetJsonAsync<BuildDetailsDto>(path, cancellationToken);

        var causes = dto.Actions?
            .SelectMany(a => a.Causes ?? Array.Empty<CauseDto>())
            .Select(c => c.ShortDescription)
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .ToArray() ?? Array.Empty<string>();

        var artifacts = dto.Artifacts?
            .Select(a => new JenkinsBuildArtifact(a.FileName ?? string.Empty, a.RelativePath ?? string.Empty))
            .ToArray() ?? Array.Empty<JenkinsBuildArtifact>();

        return new JenkinsBuildDetails(
            Number:      dto.Number,
            Url:         dto.Url ?? string.Empty,
            Building:    dto.Building,
            Result:      dto.Result,
            Timestamp:   dto.Timestamp,
            Duration:    dto.Duration,
            Description: dto.Description,
            Artifacts:   artifacts,
            Causes:      causes);
    }

    public async Task<IReadOnlyList<Build>> ListBuildsAsync(string jobName, int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return Array.Empty<Build>();

        // Jenkins's `tree` query supports `{from,to}` ranges on arrays — returns
        // the first <count> builds in one HTTP round-trip. Newest-first is
        // Jenkins's default ordering for the builds array.
        var path = $"{JobPath(jobName)}/api/json"
                 + $"?tree=builds[number,url,building,result,timestamp,duration,description]{{0,{count}}}";

        var dto = await GetJsonAsync<BuildListDto>(path, cancellationToken);
        return dto.Builds ?? Array.Empty<Build>();
    }

    public async Task<long> StartBuildAsync(
        string jobName,
        IDictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        // Prefer /buildWithParameters: works for parameterized jobs both with caller-
        // supplied values and without (Jenkins fills in defaults for missing params).
        // /build only works on non-parameterized jobs — Jenkins returns 400 with
        // "This job is parameterized" if you call /build on one that has parameters.
        var queryStr = parameters is { Count: > 0 } ? "?" + BuildQuery(parameters) : string.Empty;
        var withParamsPath = $"{JobPath(jobName)}/buildWithParameters{queryStr}";

        HttpResponseMessage? resp = null;
        try
        {
            using (var req = new HttpRequestMessage(HttpMethod.Post, withParamsPath))
            {
                resp = await SendWithCrumbAsync(req, cancellationToken);
            }

            // Non-parameterized jobs don't expose /buildWithParameters. Fall back to
            // /build, but only when the caller didn't supply parameters (we can't
            // pass params through /build).
            if ((resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
                && parameters is null or { Count: 0 })
            {
                resp.Dispose();
                using var fallbackReq = new HttpRequestMessage(HttpMethod.Post, $"{JobPath(jobName)}/build");
                resp = await SendWithCrumbAsync(fallbackReq, cancellationToken);
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"Jenkins build trigger for '{jobName}' failed: {(int)resp.StatusCode} {resp.StatusCode}. {body}".Trim(),
                    inner: null,
                    statusCode: resp.StatusCode);
            }

            var location = resp.Headers.Location
                ?? throw new InvalidOperationException("Jenkins did not return a Location header for the queued build.");
            // Location is typically: <jenkinsUrl>/queue/item/<id>/
            var segments = location.AbsolutePath.TrimEnd('/').Split('/');
            if (segments.Length < 2 || !long.TryParse(segments[^1], out var queueId))
            {
                throw new InvalidOperationException($"Unexpected queue Location: {location}");
            }
            return queueId;
        }
        finally
        {
            resp?.Dispose();
        }
    }

    public async Task<QueueItem> GetQueueItemAsync(long queueId, CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<QueueItem>($"queue/item/{queueId}/api/json", cancellationToken);
    }

    public async Task<int> WaitForBuildToStartAsync(
        long queueId,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var interval = pollInterval ?? DefaultPollInterval;
        using var timer = NewTimeoutCts(timeout, cancellationToken);

        while (true)
        {
            timer.Token.ThrowIfCancellationRequested();
            var item = await GetQueueItemAsync(queueId, timer.Token);
            if (item.Cancelled)
            {
                throw new InvalidOperationException($"Queue item {queueId} was cancelled before starting (reason: {item.Why ?? "n/a"}).");
            }
            if (item.Executable is { Number: > 0 })
            {
                return item.Executable.Number;
            }
            await Task.Delay(interval, timer.Token);
        }
    }

    public async Task<Build> GetBuildAsync(string jobName, int buildNumber, CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<Build>($"{JobPath(jobName)}/{buildNumber}/api/json", cancellationToken);
    }

    public async Task<Build> GetLastBuildAsync(string jobName, CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<Build>($"{JobPath(jobName)}/lastBuild/api/json", cancellationToken);
    }

    public async Task<Build> WaitForBuildToFinishAsync(
        string jobName,
        int buildNumber,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var interval = pollInterval ?? DefaultPollInterval;
        using var timer = NewTimeoutCts(timeout, cancellationToken);

        while (true)
        {
            timer.Token.ThrowIfCancellationRequested();
            var build = await GetBuildAsync(jobName, buildNumber, timer.Token);
            if (!build.Building)
            {
                return build;
            }
            await Task.Delay(interval, timer.Token);
        }
    }

    public async Task StopBuildAsync(string jobName, int buildNumber, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{JobPath(jobName)}/{buildNumber}/stop");
        using var resp = await SendWithCrumbAsync(req, cancellationToken);
        // Jenkins returns 302 even on success — treat anything < 400 as ok.
        if ((int)resp.StatusCode >= 400)
        {
            resp.EnsureSuccessStatusCode();
        }
    }

    public async Task DeleteBuildAsync(string jobName, int buildNumber, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{JobPath(jobName)}/{buildNumber}/doDelete");
        using var resp = await SendWithCrumbAsync(req, cancellationToken);
        // Jenkins redirects (302) to the job page on success; a missing build is 404 (already gone).
        if (resp.StatusCode == HttpStatusCode.NotFound) return;
        if ((int)resp.StatusCode >= 400) resp.EnsureSuccessStatusCode();
    }

    public async Task<string> GetConsoleLogAsync(string jobName, int buildNumber, CancellationToken cancellationToken = default)
    {
        using var resp = await _http.GetAsync($"{JobPath(jobName)}/{buildNumber}/consoleText", cancellationToken);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(cancellationToken);
    }

    public async IAsyncEnumerable<string> StreamConsoleLogAsync(
        string jobName,
        int buildNumber,
        TimeSpan? pollInterval = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var interval = pollInterval ?? TimeSpan.FromSeconds(1);
        long offset = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HttpResponseMessage resp;
            try
            {
                resp = await _http.GetAsync(
                    $"{JobPath(jobName)}/{buildNumber}/logText/progressiveHtml?start={offset}",
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
                // Transient network glitch — bail out; orchestrator will treat the loop
                // ending as the natural end of the log stream and proceed.
                yield break;
            }

            try
            {
                resp.EnsureSuccessStatusCode();

                var text = await resp.Content.ReadAsStringAsync(cancellationToken);
                if (text.Length > 0)
                {
                    yield return text;
                }

                if (resp.Headers.TryGetValues("X-Text-Size", out var sizeVals)
                    && long.TryParse(sizeVals.FirstOrDefault(), out var size))
                {
                    offset = size;
                }

                var moreData = resp.Headers.TryGetValues("X-More-Data", out var moreVals)
                    && string.Equals(moreVals.FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase);
                if (!moreData)
                {
                    yield break;
                }
            }
            finally
            {
                resp.Dispose();
            }

            try { await Task.Delay(interval, cancellationToken); }
            catch (OperationCanceledException) { yield break; }
        }
    }

    public async Task<byte[]> GetArtifactAsync(
        string jobName,
        int buildNumber,
        string artifactPath,
        CancellationToken cancellationToken = default)
    {
        var path = $"{JobPath(jobName)}/{buildNumber}/artifact/{artifactPath.TrimStart('/')}";
        using var resp = await _http.GetAsync(path, cancellationToken);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<Build> RunBuildAsync(
        string jobName,
        IDictionary<string, string>? parameters = null,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var queueId = await StartBuildAsync(jobName, parameters, cancellationToken);
        var buildNumber = await WaitForBuildToStartAsync(queueId, pollInterval, timeout, cancellationToken);
        return await WaitForBuildToFinishAsync(jobName, buildNumber, pollInterval, timeout, cancellationToken);
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
        _crumbLock.Dispose();
    }

    // --- Internals ---

    private static string JobPath(string jobName)
    {
        // Jenkins folder convention: "folderA/folderB/jobName" -> "/job/folderA/job/folderB/job/jobName"
        var parts = jobName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return "job/" + string.Join("/job/", parts);
    }

    private static string BuildQuery(IDictionary<string, string> parameters)
    {
        var sb = new StringBuilder();
        var first = true;
        foreach (var kv in parameters)
        {
            if (!first) sb.Append('&');
            sb.Append(Uri.EscapeDataString(kv.Key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(kv.Value ?? string.Empty));
            first = false;
        }
        return sb.ToString();
    }

    private static CancellationTokenSource NewTimeoutCts(TimeSpan? timeout, CancellationToken outer)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(outer);
        cts.CancelAfter(timeout ?? DefaultTimeout);
        return cts;
    }

    private async Task<T> GetJsonAsync<T>(string path, CancellationToken ct)
    {
        var result = await _http.GetFromJsonAsync<T>(path, JsonOpts, ct);
        return result ?? throw new InvalidOperationException($"Jenkins returned empty body for {path}");
    }

    /// <summary>
    /// Sends a POST and transparently attaches a CSRF crumb if Jenkins requires one. The crumb is
    /// fetched lazily on first POST and cached for the lifetime of this client. Cluster-restart or
    /// crumb-rotation will surface as a 403 on the next POST — recreate the client to recover.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithCrumbAsync(HttpRequestMessage req, CancellationToken ct)
    {
        await EnsureCrumbAsync(ct);
        if (_crumb is { } c)
        {
            req.Headers.TryAddWithoutValidation(c.Field, c.Value);
        }
        return await _http.SendAsync(req, ct);
    }

    private async Task EnsureCrumbAsync(CancellationToken ct)
    {
        if (_crumbFetched) return;
        await _crumbLock.WaitAsync(ct);
        try
        {
            if (_crumbFetched) return;
            try
            {
                using var resp = await _http.GetAsync("crumbIssuer/api/json", ct);
                if (resp.StatusCode == HttpStatusCode.NotFound)
                {
                    // CSRF disabled on this Jenkins; nothing to send.
                    _crumb = null;
                }
                else
                {
                    resp.EnsureSuccessStatusCode();
                    var doc = await resp.Content.ReadFromJsonAsync<CrumbResponse>(JsonOpts, ct);
                    if (doc is { Crumb: { Length: > 0 } crumb, CrumbRequestField: { Length: > 0 } field })
                    {
                        _crumb = (field, crumb);
                    }
                }
            }
            catch (HttpRequestException) when (!ct.IsCancellationRequested)
            {
                // Treat any crumb fetch failure as "no crumb" — Jenkins will return 403 on the actual
                // POST if it really needed one, and that error is more diagnostic than failing here.
                _crumb = null;
            }
            _crumbFetched = true;
        }
        finally
        {
            _crumbLock.Release();
        }
    }

    private sealed record CrumbResponse(string Crumb, string CrumbRequestField);

    private sealed record BuildListDto(Build[]? Builds);

    private sealed record BuildDetailsDto(
        int Number,
        string? Url,
        bool Building,
        BuildResult? Result,
        long Timestamp,
        long Duration,
        string? Description,
        ArtifactDto[]? Artifacts,
        ActionDto[]? Actions);

    private sealed record ArtifactDto(string? FileName, string? RelativePath);
    private sealed record ActionDto(CauseDto[]? Causes);
    private sealed record CauseDto(string? ShortDescription);

    // --- DTOs for ListJobs / GetJobDetails ---

    private sealed record JobListDto(JobSummaryDto[]? Jobs);

    private sealed record JobSummaryDto(
        string? Name,
        string? Url,
        string? Color,
        bool? Buildable,
        JobLastBuildDto? LastBuild);

    private sealed record JobLastBuildDto(int Number);

    private sealed record JobDetailsDto(
        string? Description,
        JobPropertyDto[]? Property);

    private sealed record JobPropertyDto(ParameterDefinitionDto[]? ParameterDefinitions);

    private sealed record ParameterDefinitionDto(
        string? Name,
        string? Type,
        string? Description,
        DefaultParameterValueDto? DefaultParameterValue,
        string[]? Choices);

    /// <summary>
    /// Jenkins encodes default-value as a heterogeneously-typed JSON node
    /// (string / bool / number / null). Keep it as JsonElement and convert
    /// to string at the consumer boundary.
    /// </summary>
    private sealed record DefaultParameterValueDto(JsonElement? Value);
}
