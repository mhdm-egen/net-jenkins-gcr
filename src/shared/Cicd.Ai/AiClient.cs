using System.Text.Json;
using Microsoft.Extensions.Logging;
using Anthropic;
using Anthropic.Models.Messages;

namespace Cicd.Ai;

/// <summary>
/// The single chokepoint for every AI call. Wraps the official Anthropic SDK so we get
/// the raw <c>usage</c> block (including the cache-read/creation breakdown the metering
/// ledger needs), captures it at this boundary, and hands it to an
/// <see cref="IAiUsageRecorder"/>. Soft-fails when unconfigured, the same convention the
/// platform's other optional integrations use: a missing API key records a
/// <see cref="ConfigurationError"/> instead of throwing at startup.
/// </summary>
public sealed class AiClient : IAiInsightService, IAiAgentService
{
    /// <summary>
    /// How many times the model may call tools before it must answer. A read-only agent can't do
    /// damage by looping, but it can spend tokens, so the loop is bounded and the UI is told when
    /// the bound was hit rather than being handed a partial answer that looks complete.
    /// </summary>
    private const int MaxToolTurns = 8;

    /// <summary>
    /// Tool results are model input. An unbounded one (a container list, a long log) would blow the
    /// context and the bill, so each is truncated with an explicit marker — the model must be able
    /// to see that it is looking at a prefix rather than the whole answer.
    /// </summary>
    private const int MaxToolResultChars = 6_000;

    private readonly AiOptions _options;
    private readonly IAiUsageRecorder _usage;
    private readonly ILogger<AiClient> _log;
    private readonly AnthropicClient? _client;

    public AiClient(AiOptions options, IAiUsageRecorder usage, ILogger<AiClient> log)
    {
        _options = options;
        _usage = usage;
        _log = log;

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            ConfigurationError =
                "Ai:ApiKey is not configured. Set env var Ai__ApiKey (double underscore). AI features are disabled.";
            return;
        }

        _client = new AnthropicClient { ApiKey = options.ApiKey };
    }

    public bool IsConfigured => _client is not null;

    public string? ConfigurationError { get; }

    public async Task<AiInsight> GetInsightAsync(AiInsightRequest request, CancellationToken ct = default)
    {
        if (_client is null)
            throw new InvalidOperationException(ConfigurationError ?? "AI is not configured.");

        var model = _options.ModelFor(request.Model);

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = model,
            MaxTokens = _options.MaxOutputTokens,
            System = request.SystemPrompt,
            Messages = [new() { Role = Role.User, Content = request.GroundedPrompt }],
        }, cancellationToken: ct);

        var text = string.Concat(
            response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));

        var usage = new AiUsage(
            InputTokens: response.Usage.InputTokens,
            OutputTokens: response.Usage.OutputTokens,
            CacheReadInputTokens: response.Usage.CacheReadInputTokens ?? 0,
            CacheCreationInputTokens: response.Usage.CacheCreationInputTokens ?? 0);

        _usage.Record(request.Feature, model, usage, request.Dimensions);

        return new AiInsight(text, usage, model);
    }

    public async Task<AiAgentResult> RunAgentAsync(AiAgentRequest request, CancellationToken ct = default)
    {
        if (_client is null)
            throw new InvalidOperationException(ConfigurationError ?? "AI is not configured.");

        var model = _options.ModelFor(request.Model);

        // Tools render at position 0 of the prompt, so their order IS part of the cache key.
        // Sorting by name makes the prefix byte-identical across requests regardless of the order
        // the registry happened to produce.
        var tools = request.Tools
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => (ToolUnion)new Tool
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = new() { Properties = t.Properties, Required = t.Required },
            })
            .ToList();

        // The cache breakpoint goes on the system block. Render order is tools -> system ->
        // messages, so one marker here caches the tool definitions AND the system prompt together —
        // which is the entire stable prefix of every request this agent ever makes.
        //
        // The system prompt must therefore stay frozen: no timestamps, no user names, no
        // conditional sections. Anything dynamic belongs in the messages, after this point.
        var system = new List<TextBlockParam>
        {
            new() { Text = request.SystemPrompt, CacheControl = new CacheControlEphemeral() },
        };

        var messages = new List<MessageParam>();
        foreach (var turn in request.History)
        {
            messages.Add(new MessageParam
            {
                Role = turn.FromUser ? Role.User : Role.Assistant,
                Content = turn.Text,
            });
        }
        messages.Add(new MessageParam { Role = Role.User, Content = request.Question });

        var invocations = new List<AiToolInvocation>();
        var total = new AiUsage(0, 0, 0, 0);
        var text = string.Empty;
        var hitLimit = false;

        for (var turn = 0; ; turn++)
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = model,
                MaxTokens = _options.MaxOutputTokens,
                System = system,
                Tools = tools,
                Messages = messages,
            }, cancellationToken: ct);

            var usage = new AiUsage(
                InputTokens: response.Usage.InputTokens,
                OutputTokens: response.Usage.OutputTokens,
                CacheReadInputTokens: response.Usage.CacheReadInputTokens ?? 0,
                CacheCreationInputTokens: response.Usage.CacheCreationInputTokens ?? 0);

            // Recorded per turn, not once at the end: each turn is a separate billable call, and
            // the ledger should show what actually happened rather than a synthetic aggregate.
            _usage.Record(request.Feature, model, usage, request.Dimensions);

            total = new AiUsage(
                total.InputTokens + usage.InputTokens,
                total.OutputTokens + usage.OutputTokens,
                total.CacheReadInputTokens + usage.CacheReadInputTokens,
                total.CacheCreationInputTokens + usage.CacheCreationInputTokens);

            text = string.Concat(
                response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));

            if (response.StopReason != StopReason.ToolUse)
                break;

            if (turn >= MaxToolTurns - 1)
            {
                hitLimit = true;
                break;
            }

            // Echo the assistant turn back verbatim, then answer every tool_use block. The API
            // rejects the follow-up if any tool_use ID lacks a matching tool_result, so this must
            // stay one-result-per-block even when a tool throws.
            var assistantContent = new List<ContentBlockParam>();
            var toolResults = new List<ContentBlockParam>();

            foreach (var block in response.Content)
            {
                if (block.TryPickText(out var textBlock))
                {
                    assistantContent.Add(new TextBlockParam { Text = textBlock.Text });
                }
                else if (block.TryPickToolUse(out var toolUse))
                {
                    assistantContent.Add(new ToolUseBlockParam
                    {
                        ID = toolUse.ID,
                        Name = toolUse.Name,
                        Input = toolUse.Input,
                    });

                    var (result, isError) = await ExecuteToolAsync(request.ToolExecutor, toolUse, ct);

                    invocations.Add(new AiToolInvocation(
                        Name: toolUse.Name,
                        Arguments: DescribeArguments(toolUse.Input),
                        IsError: isError,
                        ResultLength: result.Length));

                    toolResults.Add(new ToolResultBlockParam
                    {
                        ToolUseID = toolUse.ID,
                        Content = Truncate(result),
                        IsError = isError ? true : null,
                    });
                }
            }

            messages.Add(new MessageParam { Role = Role.Assistant, Content = assistantContent });
            messages.Add(new MessageParam { Role = Role.User, Content = toolResults });
        }

        return new AiAgentResult(text, invocations, total, model, hitLimit);
    }

    /// <summary>
    /// A failing tool must not fail the turn. The model is told what went wrong and can try a
    /// different approach — which is strictly better than the user getting an exception page
    /// because one of eight read endpoints was briefly down.
    /// </summary>
    private async Task<(string Result, bool IsError)> ExecuteToolAsync(
        AiToolExecutor executor, ToolUseBlock toolUse, CancellationToken ct)
    {
        try
        {
            return (await executor(toolUse.Name, toolUse.Input, ct), false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "AI tool {Tool} failed", toolUse.Name);
            return ($"The tool '{toolUse.Name}' failed: {ex.Message}", true);
        }
    }

    private static string Truncate(string value) =>
        value.Length <= MaxToolResultChars
            ? value
            : value[..MaxToolResultChars] +
              $"\n\n[truncated — {value.Length - MaxToolResultChars} more characters were not shown]";

    /// <summary>Compact, log-safe rendering of the arguments, for the UI's "show your work" list.</summary>
    private static string DescribeArguments(IReadOnlyDictionary<string, JsonElement> input) =>
        input.Count == 0
            ? string.Empty
            : string.Join(", ", input.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                                     .Select(kv => $"{kv.Key}={kv.Value}"));
}
