---
description: Use GitHub Copilot as a direct IChatClient provider and choose between Foundry's HTTP client and Microsoft's CLI-backed AIAgent.
---

# GitHub Copilot Integration

`NexusLabs.Foundry.Copilot` provides two capabilities:

1. **`CopilotChatClient`** — an `IChatClient` backed by the GitHub Copilot API (no CLI process, direct HTTP)
2. **`CopilotWebSearchFunction`** — an `AIFunction` wrapping Copilot's MCP `web_search` tool

Because Copilot is free for many developers, this is an excellent local-development alternative to Azure OpenAI or other paid providers.

---

## Installation

```xml
<PackageReference Include="NexusLabs.Foundry.Copilot" />
```

---

## Quick Start

### Standalone usage

```csharp
using Microsoft.Extensions.AI;
using NexusLabs.Foundry.Copilot;

// Automatically discovers your GitHub token from the Copilot CLI's apps.json
using var client = new CopilotChatClient(new CopilotChatClientOptions());

var response = await client.GetResponseAsync(
[
    new(ChatRole.User, "What is dependency injection?"),
]);

Console.WriteLine(response.Messages.Last());
```

### With Foundry and Needlr

Plug the Copilot client into Foundry's Needlr integration via the existing `UsingChatClient()` hook:

```csharp
var services = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .UsingChatClient(new CopilotChatClient(new CopilotChatClientOptions
        {
            DefaultModel = "gpt-4.1",
        }))
        .AddAgentFunctionsFromAssemblies())
    .BuildServiceProvider(configuration);
```

---

## Authentication

`CopilotChatClient` needs a GitHub OAuth token to exchange for a Copilot API token. The discovery chain (in order of precedence):

| Source | How |
|---|---|
| **Explicit token** | Set `CopilotChatClientOptions.GitHubToken` directly |
| **apps.json** | Auto-discovered from `~/.config/github-copilot/apps.json` (macOS/Linux) or `%LOCALAPPDATA%\github-copilot\apps.json` (Windows) |
| **`GH_TOKEN` env var** | Standard GitHub CLI environment variable |
| **`GITHUB_TOKEN` env var** | Fallback environment variable |

The `TokenSource` property controls which sources are tried:

```csharp
var options = new CopilotChatClientOptions
{
    // Default: tries all sources in order
    TokenSource = CopilotTokenSource.Auto,
};
```

| `CopilotTokenSource` | Behaviour |
|---|---|
| `Auto` | Explicit → apps.json → env vars |
| `AppsJson` | apps.json only |
| `EnvironmentVariable` | `GH_TOKEN` / `GITHUB_TOKEN` only |

!!! tip "Copilot CLI login"
    If you have the GitHub Copilot CLI extension installed and have run `gh copilot auth login`, the apps.json file is already populated. No additional configuration is needed.

---

## Web Search Tool

`CopilotToolSet` creates `AIFunction` instances backed by Copilot's MCP endpoint. Currently the only available tool is `web_search`:

```csharp
using NexusLabs.Foundry.Copilot;

var chatOptions = new CopilotChatClientOptions();
var tools = CopilotToolSet.Create(
    opts => opts.EnableWebSearch = true,
    chatOptions);

// tools[0] is an AIFunction named "web_search"
// Pass it to ChatOptions.Tools for any IChatClient
```

### With the Agent Framework

```csharp
var chatOptions = new CopilotChatClientOptions();
var copilotTools = CopilotToolSet.Create(
    opts => opts.EnableWebSearch = true,
    chatOptions);

var services = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .UsingChatClient(new CopilotChatClient(chatOptions))
        .AddAgentFunctionsFromAssemblies())
    .BuildServiceProvider(configuration);

var agentFactory = services.GetRequiredService<IAgentFactory>();
var agent = agentFactory.CreateAgent(opts =>
{
    opts.Instructions = "You are a research assistant with web access.";
    opts.AdditionalTools = copilotTools;
});
```

---

## Streaming

`CopilotChatClient` supports SSE streaming:

```csharp
await foreach (var update in client.GetStreamingResponseAsync(messages))
{
    Console.Write(update.Text);
}
```

Malformed SSE chunks are silently skipped — the stream continues without interruption.

---

## Configuration Reference

All settings are on `CopilotChatClientOptions`:

| Property | Default | Description |
|---|---|---|
| `DefaultModel` | `"gpt-4.1"` | Model to use when `ChatOptions.ModelId` is not set |
| `CopilotApiBaseUrl` | `"https://api.githubcopilot.com"` | Base URL for the Copilot chat API |
| `GitHubApiBaseUrl` | `"https://api.github.com"` | Base URL for the GitHub API (token exchange) |
| `IntegrationId` | `"copilot-developer-cli"` | Sent as `Copilot-Integration-Id` header |
| `GitHubToken` | `null` | Explicit GitHub OAuth token (bypasses discovery) |
| `TokenSource` | `Auto` | Which token sources to try |
| `MaxRetries` | `3` | Maximum retry attempts for 429 responses |
| `RetryBaseDelayMs` | `1000` | Base delay for exponential backoff (ms) |
| `TokenRefreshBufferSeconds` | `60` | Refresh the Copilot token this many seconds before expiry |

---

## Retry Behaviour

The client retries only on HTTP 429 (Too Many Requests):

- Uses the `Retry-After` header when present
- Falls back to exponential backoff: `RetryBaseDelayMs * 2^attempt`
- Gives up after `MaxRetries` attempts

Other transient HTTP errors (5xx) surface as `HttpRequestException` with the upstream status code in the message.

---

## Typed Exceptions

The SDK surfaces two failure modes as typed exceptions so consumers can catch them without parsing message text. Everything else throws `HttpRequestException` (transport/5xx) or other framework exceptions.

| Exception | Trigger | Notes |
|---|---|---|
| `CopilotRateLimitException` | HTTP 429 after retry exhaustion, or rate-limit prose detected in a successful MCP response body | Carries `RetryAfter` parsed from the `Retry-After` header or message text |
| `CopilotAuthException` | (a) `GitHubOAuthTokenProvider` cannot resolve a token from the configured source(s), or (b) `CopilotMcpToolClient` receives HTTP 401 or 403 from the MCP endpoint | Indicates the GitHub OAuth token is missing, expired, invalid, or lacks the required scopes |

`CopilotWebSearchFunction.InvokeCoreAsync` lets both exceptions propagate to the caller. Other failures inside the function are converted into a `"Web search failed: …"` text return so the agent loop can keep going — but auth and rate-limit cases break out so consumers can route to a fallback provider or surface a re-auth prompt.

```csharp
try
{
    var result = await searchFunction.InvokeAsync(args, ct);
    // ...
}
catch (CopilotAuthException ex)
{
    // Token missing / expired / rejected. Prompt the user to re-authenticate.
    throw new ProviderUnavailableException("copilot", ex.Message, ex);
}
catch (CopilotRateLimitException ex)
{
    // Fall through to an alternative search provider; honour ex.RetryAfter.
    throw new ProviderUnavailableException("copilot", ex.Message, ex);
}
```

---

## Choosing a Copilot integration

The official
[`Microsoft.Agents.AI.GitHub.Copilot`](https://www.nuget.org/packages/Microsoft.Agents.AI.GitHub.Copilot)
package is complementary to `NexusLabs.Foundry.Copilot`, not a replacement.
They expose different abstractions and different runtime semantics:

| Capability | `NexusLabs.Foundry.Copilot` | `Microsoft.Agents.AI.GitHub.Copilot` |
|---|---|---|
| Primary contract | `IChatClient` | `AIAgent` |
| Runtime | Direct HTTP | Copilot CLI process/runtime |
| Conversation state | Caller supplies message history | Persistent/resumable SDK session |
| Per-request options | `ChatOptions` model, sampling, stops, tools | Session-level `SessionConfig`; `AgentRunOptions` are not mapped |
| Input message semantics | Roles and tool messages preserved | Message text is joined into one prompt; session holds history |
| Function tools | Caller-supplied only | Caller tools plus permission-gated CLI capabilities |
| Shell/file/URL capabilities | None | Built into the CLI runtime |
| MCP servers | Copilot read-only web-search endpoint only | Local stdio and remote HTTP servers |
| Structured web citations | `WebSearchResult.Citations` and `SearchQueries` | Not exposed by the MAF adapter |
| Sessions, memory, skills, compaction | Caller/Foundry owned | SDK-native |
| Child process | None | Default; remote/in-process connections are also available |
| Model selection | Direct API IDs, per request | CLI catalog, per session; supports `auto` |
| NativeAOT | Live text and dictionary-result tool loop verified | Live chat and custom tool call verified under NativeAOT |
| Deployment footprint | Managed client only | Native host plus downloaded Copilot runtime (~162 MB in a measured win-x64 AOT publish) |

### Use Foundry Copilot when

- **You need `IChatClient`.** It plugs into MEAI and Foundry middleware without
  replacing the agent construction or tool loop.
- **You need normal chat-history semantics.** System, user, assistant, and tool
  messages are sent with their roles intact.
- **You need per-request control.** `ChatOptions.ModelId`, sampling options,
  stop sequences, and tools apply to each call.
- **You need structured search results.** `WebSearchResult` exposes citations,
  character offsets, and the search queries to application code.
- **You cannot spawn a CLI process** or accept its deployment footprint.

### Use the first-party MAF Copilot agent when

- **You want the full Copilot CLI agent runtime.** It provides permission-gated
  shell, file, URL, skill, memory, and MCP behavior.
- **You need persistent SDK sessions.** Sessions can be serialized and resumed by
  ID, with infinite-session compaction and workspace state.
- **You need native Copilot hooks and approvals.** `ApprovalRequiredAIFunction`
  is bridged to the SDK's pre-tool permission hook.
- **You want the CLI's current model catalog.** Query `ListModelsAsync()` or use
  `Model = "auto"` rather than assuming a direct-API model ID is available.

### Model IDs are not portable

The two paths use different model catalogs. In a measured run:

- the direct API accepted `gpt-4.1`;
- the SDK runtime rejected `gpt-4.1` as unavailable;
- the SDK accepted `auto` and exposed its current GPT-5.x, Claude, Gemini, Grok,
  and MAI catalog through `ListModelsAsync()`.

Do not configure both paths with one shared model string without checking both
catalogs.

### Runtime and security

The first-party package's transitive build targets download a platform-specific
Copilot runtime from npm and copy it to `runtimes/<rid>/native`. Shell, file, URL,
and MCP capabilities must be governed by a permission handler. Microsoft recommends
running agents with shell or file permissions inside a container or similarly
restricted environment.

`CopilotChatClient` has no child process, but its token exchange uses GitHub's
`/copilot_internal/v2/token` endpoint. That direct integration is lighter and more
provider-neutral, but Foundry owns its compatibility risk.

The runnable `CopilotComparisonExample` demonstrates the direct client and the
underlying SDK runtime used by Microsoft's `AIAgent` adapter. It intentionally
uses `gpt-4.1` for the direct client and `auto` for the SDK runtime.

### NativeAOT tool-result contract

`NexusLabs.Foundry.Copilot` is marked NativeAOT-compatible and is published and
executed by CI through `AotCopilotApp`. The fixture covers:

- tool schema serialization;
- model-supplied argument deserialization;
- a dictionary-valued tool result;
- the second provider request and final response.

Structured tool arguments and results must use shapes registered in
`CopilotJsonContext`, such as `JsonElement`, dictionaries with string keys, common
numeric/temporal primitives, or the registered array/list shapes. An arbitrary CLR
object whose runtime type has no generated JSON metadata falls back to `ToString()`
instead of reflection serialization. The default record/class `ToString()` is usually
not useful to a model; return a `JsonElement` or dictionary when it needs structured
fields from a custom result type.

---

## Web Search Limitations

!!! warning "web_search is not a search engine"
    The Copilot `web_search` tool is **not a reliable web search provider**. It is an LLM-mediated endpoint that may or may not trigger a Bing search depending on the query.

**How it actually works:**

1. Your query is sent to the Copilot MCP server
2. An LLM on the server side evaluates the query
3. The LLM **decides** whether it needs web data or can answer from training knowledge
4. If it searches, the response includes structured `annotations` (citations with URLs) and `bing_searches` (the queries it ran)
5. If it doesn't search, the response is training-data-sourced text with no citations

**What this means in practice:**

| Query type | LLM behaviour | Citations? |
|---|---|---|
| Time-sensitive, specific, factual | Triggers Bing search | ✅ Real URLs with titles |
| General knowledge ("what is dependency injection?") | Answers from training data | ❌ None |
| General but LLM feels helpful | Answers from training data, may embed inline URLs from memory | ❌ No structured citations (inline URLs may be hallucinated) |

**There is no way to force a web search.** The LLM decides. Phrasing queries with time-sensitive language ("in 2026", "latest", "current") increases the likelihood of triggering a search, but it's never guaranteed.

**Implications for tiered providers:**

If your use case requires **guaranteed web search with verifiable sources**, use a real search API (Bing Web Search API, DuckDuckGo, Google Custom Search) as your primary provider. Copilot's `web_search` is better suited as a synthesis/fallback provider that sometimes includes grounded citations.

`WebSearchResult.Citations.Count == 0` does not mean the search failed — it means the LLM answered from training data. The `Text` may still be accurate; it's just not verifiable from a source URL.
