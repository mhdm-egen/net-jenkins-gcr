using Anthropic;
using Anthropic.Models.Messages;

namespace Cicd.Web.Admin.Services.Ai;

/// <summary>
/// The single chokepoint for every AI call. Wraps the official Anthropic SDK so we get
/// the raw <c>usage</c> block (including the cache-read/creation breakdown the metering
/// ledger needs), captures it at this boundary, and hands it to an
/// <see cref="IAiUsageRecorder"/>. Soft-fails when unconfigured — mirrors
/// <see cref="Nexus.NexusClient"/>: a missing API key records a
/// <see cref="ConfigurationError"/> instead of throwing at startup.
/// </summary>
public sealed class AiClient : IAiInsightService
{
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
}
