namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// Internal constants for the <c>gen_ai.token.type</c> tag values Foundry emits on the
/// <c>gen_ai.client.token.usage</c> histogram. Centralised so that label-value drift
/// between middleware, tests, and docs is impossible by accident, and so that any future
/// rename driven by OpenTelemetry semantic-convention spec changes is a single-file edit.
/// </summary>
/// <remarks>
/// <para>
/// Foundry DELIBERATELY does NOT define constants for <c>"input"</c> or <c>"output"</c> —
/// those token types are emitted by MEAI's <see cref="Microsoft.Extensions.AI.OpenTelemetryChatClient"/>
/// and Foundry must not duplicate them.
/// </para>
/// <para>
/// Neither <c>cache_read</c> nor <c>reasoning</c> is in the OpenTelemetry GenAI semantic
/// conventions <c>gen_ai.token.type</c> enum as of v1.41 (the version MEAI 10.5.0
/// implements). They are pragmatic Foundry extensions to surface data that exists in
/// <see cref="Microsoft.Extensions.AI.UsageDetails.CachedInputTokenCount"/> and
/// <see cref="Microsoft.Extensions.AI.UsageDetails.ReasoningTokenCount"/> respectively.
/// </para>
/// </remarks>
internal static class GenAiTokenTypes
{
    /// <summary>
    /// <c>gen_ai.token.type</c> value for tokens served from a provider's prompt cache
    /// (e.g. Anthropic prompt-cache hits, Azure OpenAI cached input). Sourced from
    /// <see cref="Microsoft.Extensions.AI.UsageDetails.CachedInputTokenCount"/>.
    /// </summary>
    public const string CacheRead = "cache_read";

    /// <summary>
    /// <c>gen_ai.token.type</c> value for tokens spent on internal reasoning that are
    /// reported separately from final completion tokens (e.g. o-series, Claude extended-
    /// thinking). Sourced from <see cref="Microsoft.Extensions.AI.UsageDetails.ReasoningTokenCount"/>.
    /// </summary>
    public const string Reasoning = "reasoning";
}
