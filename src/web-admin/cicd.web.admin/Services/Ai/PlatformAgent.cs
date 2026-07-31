using Cicd.Ai;

namespace Cicd.Web.Admin.Services.Ai;

public interface IPlatformAgent
{
    /// <summary>False when no API key is configured — callers hide the affordance rather than fail.</summary>
    bool IsConfigured { get; }

    Task<PlatformAnswer> AskAsync(
        string question, IReadOnlyList<AiAgentTurn> history, CancellationToken ct = default);
}

/// <summary>
/// The agent's answer plus any actions it suggested. Proposals are carried separately from the
/// text because they are rendered as links to real confirm gates, not as prose.
/// </summary>
public sealed record PlatformAnswer(AiAgentResult Result, IReadOnlyList<ActionProposal> Proposals);

/// <summary>
/// "Ask the platform" — the agentic slice. Unlike every other AI feature here, the prompt is not
/// assembled in advance from data the page already had: the model is given a read-only tool surface
/// and decides what to look at.
///
/// Two things hold that in place:
///
/// **Read-only.** <see cref="PlatformToolRegistry"/> exposes reads and nothing else. Writes keep
/// going through the existing endpoints behind the existing confirm gates.
///
/// **Bounded.** The loop caps tool turns, and each tool result is capped and truncated with a
/// visible marker. An agent that can't stop is a cost problem even when it can't be a safety one.
///
/// This is also the first feature with a **large stable prompt prefix** — a frozen system prompt
/// plus sixteen tool definitions — which is why prompt caching is enabled here and why the
/// cache-hit-rate tile on /ai/usage stops reading a structural zero.
/// </summary>
public sealed class PlatformAgent : IPlatformAgent
{
    // FROZEN. This string is the front of the cached prefix on every request the agent makes:
    // interpolating anything per-request (a date, a user, a repository name) would invalidate the
    // cache for every call and quietly make the caching work pointless. Dynamic context belongs in
    // the question, which is appended after the breakpoint.
    private const string SystemPrompt =
        "You are a read-only assistant embedded in a CI/CD platform. The platform builds Git " +
        "repositories with Jenkins, publishes artifacts and containers to Sonatype Nexus, and " +
        "deploys them either to Google Cloud Run (individual services) or to Kubernetes (whole " +
        ".NET Aspire applications, plus ephemeral per-PR preview environments).\n\n" +

        "You answer questions about what this platform has actually done by calling the tools you " +
        "have been given. Those tools are the only source of truth available to you.\n\n" +

        "How to work:\n" +
        "- Call tools to find out. Do not answer from memory or assumption about how a CI/CD " +
        "platform 'usually' works — answer about THIS one, from what the tools return.\n" +
        "- Most lookups need an id first. Repositories, pipelines, services and Aspire applications " +
        "are listed by their own tools; use those to resolve a name the user gave you into an id.\n" +
        "- Prefer the smallest tool that answers the question. Console output in particular is " +
        "large — a run's step record usually says which job failed without it.\n" +
        "- Tool results may be truncated, and say so when they are. Never present a truncated list " +
        "as if it were complete.\n\n" +

        "How to answer:\n" +
        "- Ground every claim in tool output. If the tools do not show something, say that you " +
        "cannot see it rather than inferring it. Never invent build numbers, commit SHAs, CVE ids, " +
        "timestamps, service names, or error text.\n" +
        "- If a tool fails or returns nothing, say so plainly and say what you tried.\n" +
        "- Distinguish what the data shows from what you think it means, and be explicit about " +
        "which you are doing.\n" +
        "- Watch sample sizes on metrics. A mean over two data points is not a trend, and the DORA " +
        "summary reports its sample counts precisely so you do not over-read it.\n" +
        "- Be concise and concrete. Lead with the answer, then the evidence.\n\n" +

        "What you cannot do: you have no ability to deploy, roll back, approve, cancel, or change " +
        "anything. If the user asks you to, explain what you found and tell them where in the UI " +
        "the action lives — do not imply you performed it.\n\n" +

        "Proposing an action: when the evidence supports a specific action on a deployment run, you " +
        "may call propose_action. Be clear about what that does and does not mean. It does NOT " +
        "carry the action out — it records a suggestion, and the user sees a link to the page where " +
        "they can do it themselves after the usual confirmation. Say 'I suggest' and never 'I have' " +
        "or 'I will'. Propose only when you have specific evidence, name that evidence in the " +
        "reason, and propose one action rather than a menu. If the call is refused because the " +
        "run's status does not offer that action, accept the refusal and tell the user plainly — do " +
        "not retry with a different action hoping one sticks, and never describe a refused proposal " +
        "as if it had been recorded.";

    private readonly IAiAgentService _ai;
    private readonly PlatformToolRegistry _tools;

    public PlatformAgent(IAiAgentService ai, PlatformToolRegistry tools)
    {
        _ai = ai;
        _tools = tools;
    }

    public bool IsConfigured => _ai.IsConfigured;

    public async Task<PlatformAnswer> AskAsync(
        string question, IReadOnlyList<AiAgentTurn> history, CancellationToken ct = default)
    {
        // The registry lives for the whole circuit; proposals belong to one question.
        _tools.ResetProposals();

        var result = await _ai.RunAgentAsync(new AiAgentRequest(
            Feature: "ask_platform",
            SystemPrompt: SystemPrompt,
            History: history,
            Question: question,
            Tools: _tools.Definitions,
            ToolExecutor: _tools.ExecuteAsync,
            Model: AiModelKind.Synthesis), ct);

        return new PlatformAnswer(result, _tools.Proposals.ToList());
    }
}
