using System.Text.Json;

namespace Cicd.Ai;

/// <summary>
/// A tool the model may call. Deliberately declarative — the definition carries no behaviour, so
/// the set of tools can be rendered into a request without the executor being in scope.
/// </summary>
public sealed record AiToolDefinition(
    string Name,
    string Description,
    IReadOnlyDictionary<string, JsonElement> Properties,
    IReadOnlyList<string> Required);

/// <summary>
/// Executes a tool call by name. Returns the tool's textual result; throwing is also fine — the
/// runner converts an exception into an error result so the model can recover rather than the whole
/// turn failing.
/// </summary>
public delegate Task<string> AiToolExecutor(
    string toolName, IReadOnlyDictionary<string, JsonElement> arguments, CancellationToken ct);

/// <summary>One tool call the model actually made, recorded so the UI can show its work.</summary>
public sealed record AiToolInvocation(string Name, string Arguments, bool IsError, int ResultLength);

/// <summary>
/// A prior exchange, collapsed to text. Tool calls from earlier turns are deliberately NOT
/// replayed: the model can call a tool again if it needs the data, and replaying every tool result
/// would grow the context without bound across a long conversation.
/// </summary>
public sealed record AiAgentTurn(bool FromUser, string Text);

public sealed record AiAgentRequest(
    string Feature,
    string SystemPrompt,
    IReadOnlyList<AiAgentTurn> History,
    string Question,
    IReadOnlyList<AiToolDefinition> Tools,
    AiToolExecutor ToolExecutor,
    AiModelKind Model = AiModelKind.Synthesis,
    IReadOnlyDictionary<string, string>? Dimensions = null);

public sealed record AiAgentResult(
    string Text,
    IReadOnlyList<AiToolInvocation> ToolCalls,
    AiUsage Usage,
    string ModelUsed,
    bool HitTurnLimit);

/// <summary>
/// An agentic, tool-using request. Separate from <see cref="IAiInsightService"/> because the
/// shapes genuinely differ — one call versus a loop — but implemented by the same class so the
/// Anthropic SDK still has exactly one call site.
/// </summary>
public interface IAiAgentService
{
    bool IsConfigured { get; }

    Task<AiAgentResult> RunAgentAsync(AiAgentRequest request, CancellationToken ct = default);
}
