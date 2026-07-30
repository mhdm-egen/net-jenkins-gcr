using System.Text.Encodings.Web;
using System.Text.Json;
using Cicd.Ai;
using Cicd.Web.Admin.Services.Ci;
using Cicd.Web.Admin.Services.Deployment;
using Deployment.Contracts.AspireApps;
using Deployment.Contracts.Runs;

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

        // Tool results are read by a model as text, not embedded in a page. The default encoder
        // escapes quotes, ampersands and every non-ASCII character to \uXXXX, which makes a commit
        // message or a service name harder to read and costs tokens to say the same thing. Relaxed
        // escaping is safe here because nothing on this path is rendered as HTML — the UI shows tool
        // names and arguments only, through Blazor, which escapes them itself.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
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

            // Not a write. See ProposeActionAsync — this records a suggestion and returns it to
            // the model; the platform is not touched.
            ["propose_action"] = async (a, ct) =>
                await ProposeActionAsync(a, ct),
        };
    }

    /// <summary>
    /// Proposals the agent made during this request, in order. The UI renders them as links to the
    /// pages where the real confirm gates live.
    /// </summary>
    public IReadOnlyList<ActionProposal> Proposals => _proposals;

    private readonly List<ActionProposal> _proposals = [];

    /// <summary>
    /// Clears proposals from a previous question. The registry is scoped to the Blazor circuit, not
    /// to one question, so without this a proposal from three questions ago would still be on
    /// screen — attached to an answer that never made it.
    /// </summary>
    public void ResetProposals() => _proposals.Clear();

    /// <summary>
    /// Records a suggested action after checking it is actually applicable.
    ///
    /// **This does not perform the action and cannot.** It re-fetches the target and tests the same
    /// status condition that decides whether the button renders on the page at all — so the agent
    /// cannot propose promoting a run that already succeeded, or approving one that was never
    /// waiting for approval. A refusal comes back as text the model can act on, which is what stops
    /// it inventing a plausible-sounding action the platform would reject.
    ///
    /// Keeping validation here rather than in the UI means the model learns immediately that a
    /// proposal was rejected, instead of the user discovering it by clicking through to a page with
    /// no button on it.
    /// </summary>
    private async Task<object> ProposeActionAsync(
        IReadOnlyDictionary<string, JsonElement> a, CancellationToken ct)
    {
        var rawAction = Str(a, "action");
        var reason = Str(a, "reason");
        var runId = Guid(a, "run_id");

        if (!Enum.TryParse<ProposedActionKind>(rawAction, ignoreCase: true, out var kind)
            || kind == ProposedActionKind.TeardownPreview)
        {
            return new ProposalOutcome(false,
                $"'{rawAction}' is not a proposable action. Valid actions are: promote, rollback, " +
                "approve, reject.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ProposalOutcome(false,
                "A proposal needs a reason explaining why, grounded in what you found.");
        }

        // Try the service-deploy side first, then the Aspire side. A run id belongs to exactly one.
        var outcome = await ValidateServiceRunAsync(kind, runId, reason, ct)
                      ?? await ValidateAspireRunAsync(kind, runId, reason, ct)
                      ?? new ProposalOutcome(false,
                          $"No deployment run or Aspire run exists with id {runId}.");

        if (outcome is { Accepted: true, Proposal: { } proposal })
        {
            _proposals.Add(proposal);
        }

        return outcome;
    }

    private async Task<ProposalOutcome?> ValidateServiceRunAsync(
        ProposedActionKind kind, Guid runId, string reason, CancellationToken ct)
    {
        var run = await _deployment.GetRunAsync(runId, ct);
        if (run is null) return null;

        var label = $"{run.ServiceName} {run.Version} (deployment run)";

        // Mirrors the guard on RunDetail.razor: Promote and Rollback exist only while a run is
        // parked awaiting promotion.
        if (run.Status != DeploymentRunStatusDto.AwaitingPromotion)
        {
            return new ProposalOutcome(false,
                $"Deployment run {runId} is '{run.Status}', not 'AwaitingPromotion', so it offers " +
                "no promote or rollback action. Do not propose one.");
        }

        if (kind is not (ProposedActionKind.Promote or ProposedActionKind.Rollback))
        {
            return new ProposalOutcome(false,
                $"A service deployment run supports promote and rollback only, not '{kind}'. " +
                "Approve and reject apply to Aspire runs awaiting approval.");
        }

        return new ProposalOutcome(true,
            $"Proposal recorded: {kind} {label}. The user must carry it out; you have not.",
            new ActionProposal(kind, label, reason, $"/deployment/runs/{runId}"));
    }

    private async Task<ProposalOutcome?> ValidateAspireRunAsync(
        ProposedActionKind kind, Guid runId, string reason, CancellationToken ct)
    {
        var run = await _deployment.GetAspireRunByIdAsync(runId, ct);
        if (run is null) return null;

        var label = $"{run.ApplicationName} → {run.EnvironmentName} (Aspire run)";

        // Mirrors the guard on AspireRunDetail.razor, which splits by status: approve/reject while
        // awaiting approval, promote/rollback while awaiting promotion.
        var allowed = run.Status switch
        {
            AspireRunStatusDto.AwaitingApproval  => new[] { ProposedActionKind.Approve, ProposedActionKind.Reject },
            AspireRunStatusDto.AwaitingPromotion => [ProposedActionKind.Promote, ProposedActionKind.Rollback],
            _                                    => [],
        };

        if (allowed.Length == 0)
        {
            return new ProposalOutcome(false,
                $"Aspire run {runId} is '{run.Status}', which offers no action. Do not propose one.");
        }

        if (!allowed.Contains(kind))
        {
            return new ProposalOutcome(false,
                $"Aspire run {runId} is '{run.Status}', which allows only " +
                $"{string.Join(" or ", allowed).ToLowerInvariant()} — not '{kind}'.");
        }

        return new ProposalOutcome(true,
            $"Proposal recorded: {kind} {label}. The user must carry it out; you have not.",
            new ActionProposal(kind, label, reason, $"/deployment/aspire-runs/{runId}"));
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

        // NOT a write tool. It records a suggestion for a human and returns whether the suggestion
        // was even valid; the platform is untouched either way.
        Tool("propose_action",
            "Suggest that the user carry out an action on a deployment run. This does NOT perform " +
            "the action — you cannot perform actions. It records a suggestion, which the user sees " +
            "as a link to the page where they can do it themselves, and it tells you whether the " +
            "action was applicable at all. Only propose something you have evidence for, and give " +
            "the reason you found. If the run's status does not offer the action this call is " +
            "refused and you must not claim otherwise.",
            props: [("run_id", "string", "The deployment run or Aspire run to act on."),
                    ("action", "string", "One of: promote, rollback, approve, reject."),
                    ("reason", "string", "Why, grounded in what you found. Shown to the user.")],
            required: ["run_id", "action", "reason"]),
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

    private static string Str(IReadOnlyDictionary<string, JsonElement> a, string key) =>
        a.TryGetValue(key, out var value)
            ? (value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString()).Trim()
            : "";

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
