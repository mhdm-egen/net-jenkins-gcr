using System.Text.Json;
using Cicd.Ai;
using Cicd.Web.Admin.Services.Ci;
using Cicd.Web.Admin.Services.Deployment;

namespace Cicd.Web.Admin.Services.Ai;

/// <summary>
/// The read-only tool surface the "Ask the platform" agent is given.
///
/// **Every tool here is a read.** There is no write tool, and adding one is not a small change:
/// deploys, rollbacks, approvals and deletions all sit behind confirm gates in the UI for reasons
/// that don't stop applying because the caller is a model. The agent's job is to find and explain
/// what is already true.
///
/// Each tool wraps a method the admin UI already calls, so the agent can never see more than a
/// human with the same page open — no new endpoints, no elevated access.
/// </summary>
public sealed class PlatformToolRegistry
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    /// <summary>
    /// Per-tool row cap. Tool results are model input, and an unbounded list is both a token bill
    /// and a way to bury the relevant row. The agent can narrow with arguments if it needs more.
    /// </summary>
    private const int MaxRows = 40;

    private readonly JenkinsApiClient _jenkins;
    private readonly DeploymentApiClient _deployment;
    private readonly IReadOnlyDictionary<string, Func<IReadOnlyDictionary<string, JsonElement>, CancellationToken, Task<object?>>> _handlers;

    public PlatformToolRegistry(JenkinsApiClient jenkins, DeploymentApiClient deployment)
    {
        _jenkins = jenkins;
        _deployment = deployment;

        _handlers = new Dictionary<string, Func<IReadOnlyDictionary<string, JsonElement>, CancellationToken, Task<object?>>>(StringComparer.Ordinal)
        {
            ["list_repositories"] = async (_, ct) =>
                await _jenkins.ListRepositoriesAsync(ct: ct),

            ["list_builds"] = async (a, ct) =>
                (await _jenkins.ListBuildsAsync(Guid(a, "repository_id"), Take(a), ct)).Take(MaxRows),

            ["get_build"] = async (a, ct) =>
                await _jenkins.GetBuildAsync(Guid(a, "build_id"), ct),

            ["list_pipelines"] = async (_, ct) =>
                await _jenkins.ListPipelinesAsync(ct),

            ["list_pipeline_runs"] = async (a, ct) =>
                (await _jenkins.ListPipelineRunsAsync(OptionalGuid(a, "pipeline_id"), Take(a), ct)).Take(MaxRows),

            ["get_pipeline_run"] = async (a, ct) =>
                await _jenkins.GetPipelineRunAsync(Guid(a, "run_id"), ct),

            ["get_pipeline_run_console"] = async (a, ct) =>
                await _jenkins.GetPipelineRunConsoleAsync(Guid(a, "run_id"), ct),

            ["list_services"] = async (_, ct) =>
                await _deployment.ListServicesAsync(ct),

            ["list_environments"] = async (_, ct) =>
                await _deployment.ListEnvironmentsAsync(ct),

            ["list_deployment_runs"] = async (a, ct) =>
                (await _deployment.ListRunsAsync(OptionalGuid(a, "service_id"), ct: ct)).Take(MaxRows),

            ["get_deployment_run"] = async (a, ct) =>
                await _deployment.GetRunAsync(Guid(a, "run_id"), ct),

            ["list_aspire_apps"] = async (_, ct) =>
                await _deployment.ListAspireAppsAsync(ct),

            ["list_aspire_runs"] = async (a, ct) =>
                (await _deployment.ListAspireRunsAsync(OptionalGuid(a, "application_id"), ct)).Take(MaxRows),

            ["list_previews"] = async (_, ct) =>
                await _deployment.ListPreviewsAsync(ct: ct),

            ["get_dora_summary"] = async (a, ct) =>
                await _deployment.GetDoraSummaryAsync(Days(a), ct),
        };
    }

    /// <summary>
    /// The tool definitions handed to the model. Sorted by name so the rendered prompt prefix — of
    /// which tools are position 0 — is byte-identical between requests and the cache can hit.
    /// </summary>
    public IReadOnlyList<AiToolDefinition> Definitions { get; } =
    [
        Tool("list_repositories",
            "List every source repository registered with the CI system, with its id, name, git URL, " +
            "default branch, Jenkins job name and whether it is active. Start here when you need a " +
            "repository id for another tool."),

        Tool("list_builds",
            "List CI builds for one repository, newest first. Each build carries its build number, " +
            "commit, branch, package version, status and timings — and, for builds run recently " +
            "enough, the commit author and message. Requires a repository id from list_repositories.",
            props: [("repository_id", "string", "The repository's GUID."),
                    ("take", "integer", "How many builds to return. Defaults to 25, capped at 40.")],
            required: ["repository_id"]),

        Tool("get_build",
            "Full detail for one build: versions, SBOM and vulnerability report URLs, trigger, " +
            "duration, and the artifacts it produced.",
            props: [("build_id", "string", "The build's GUID, from list_builds.")],
            required: ["build_id"]),

        Tool("list_pipelines",
            "List the orchestrator pipelines — the named chains of Jenkins jobs the platform can run."),

        Tool("list_pipeline_runs",
            "List pipeline runs, newest first, with status and failure reason. This is the place to " +
            "look for 'what failed recently'.",
            props: [("pipeline_id", "string", "Optional: restrict to one pipeline."),
                    ("take", "integer", "How many runs to return. Defaults to 25, capped at 40.")]),

        Tool("get_pipeline_run",
            "One pipeline run in detail: every step, its job, result and timings, plus the run's " +
            "failure reason if it failed.",
            props: [("run_id", "string", "The pipeline run's GUID.")],
            required: ["run_id"]),

        Tool("get_pipeline_run_console",
            "The captured Jenkins console output for a pipeline run, one segment per job. This is " +
            "large — only call it when a run's step record does not already answer the question.",
            props: [("run_id", "string", "The pipeline run's GUID.")],
            required: ["run_id"]),

        Tool("list_services",
            "List the deployable services (the Cloud Run side) with their registry and image settings."),

        Tool("list_environments",
            "List deployment environments, including which are protected by an approval gate."),

        Tool("list_deployment_runs",
            "List service deployment runs, newest first: status, target, the typed failure category " +
            "when one failed, and the step record.",
            props: [("service_id", "string", "Optional: restrict to one service."),
                    ("take", "integer", "Unused; runs are capped at 40.")]),

        Tool("get_deployment_run",
            "One service deployment run in detail, including its per-step results and failure kind.",
            props: [("run_id", "string", "The deployment run's GUID.")],
            required: ["run_id"]),

        Tool("list_aspire_apps",
            "List the registered .NET Aspire applications deployed to Kubernetes, with their source " +
            "key, environment and main branch."),

        Tool("list_aspire_runs",
            "List Aspire application deployment runs, newest first, with status and the aspirate log " +
            "reference.",
            props: [("application_id", "string", "Optional: restrict to one Aspire application.")]),

        Tool("list_previews",
            "List active preview environments — the ephemeral per-PR/branch deploys — with their " +
            "namespace, URL and expiry."),

        Tool("get_dora_summary",
            "The DORA four for a window: deployment frequency, change failure rate, lead time " +
            "(commit to production) and time to restore, each with its sample count. Read the " +
            "sample counts before drawing conclusions — a mean over two samples is not a trend.",
            props: [("days", "integer", "Window length in days. Defaults to 30.")]),
    ];

    public async Task<string> ExecuteAsync(
        string toolName, IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken ct)
    {
        if (!_handlers.TryGetValue(toolName, out var handler))
        {
            // Reachable if a definition and a handler drift apart. Telling the model is better than
            // throwing: it can pick a different tool.
            return $"No such tool: '{toolName}'.";
        }

        var result = await handler(arguments, ct);

        return result is null
            ? "No matching record was found."
            : JsonSerializer.Serialize(result, Json);
    }

    // --- definition helper ---------------------------------------------------------------------

    private static AiToolDefinition Tool(
        string name,
        string description,
        (string Name, string Type, string Description)[]? props = null,
        string[]? required = null)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (var (pName, pType, pDesc) in props ?? [])
        {
            properties[pName] = JsonSerializer.SerializeToElement(
                new { type = pType, description = pDesc });
        }

        return new AiToolDefinition(name, description, properties, required ?? []);
    }

    // --- argument coercion ---------------------------------------------------------------------
    //
    // Models pass a GUID as a JSON string and an integer as either a number or a string, depending
    // on the day. These coerce rather than trust the JSON type, and report a usable message on
    // failure so the model can correct itself instead of the turn dying.

    private static Guid Guid(IReadOnlyDictionary<string, JsonElement> a, string key)
    {
        if (!a.TryGetValue(key, out var value))
            throw new ArgumentException($"Required argument '{key}' was not supplied.");

        var raw = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();

        return System.Guid.TryParse(raw, out var parsed)
            ? parsed
            : throw new ArgumentException($"Argument '{key}' is not a valid GUID: '{raw}'.");
    }

    private static Guid? OptionalGuid(IReadOnlyDictionary<string, JsonElement> a, string key)
    {
        if (!a.TryGetValue(key, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        var raw = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();

        return string.IsNullOrWhiteSpace(raw) ? null
             : System.Guid.TryParse(raw, out var parsed) ? parsed
             : throw new ArgumentException($"Argument '{key}' is not a valid GUID: '{raw}'.");
    }

    private static int Take(IReadOnlyDictionary<string, JsonElement> a) => Int(a, "take", 25, MaxRows);

    private static int Days(IReadOnlyDictionary<string, JsonElement> a) => Int(a, "days", 30, 365);

    private static int Int(IReadOnlyDictionary<string, JsonElement> a, string key, int fallback, int max)
    {
        if (!a.TryGetValue(key, out var value)) return fallback;

        var parsed = value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out var n) ? n : fallback,
            JsonValueKind.String => int.TryParse(value.GetString(), out var s) ? s : fallback,
            _ => fallback,
        };

        return parsed <= 0 ? fallback : Math.Min(parsed, max);
    }
}
