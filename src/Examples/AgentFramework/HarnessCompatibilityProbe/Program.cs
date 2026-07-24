using HarnessCompatibilityProbe;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework;

if (!AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(out var functionProvider) ||
    !functionProvider.TryGetFunctions(
        typeof(ProbeFunctions),
        ProbeServiceProvider.Instance,
        out var functions) ||
    functions.Count != 1 ||
    !string.Equals(functions[0].Name, "Echo", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Generated tool provider was not available.");
    return 1;
}

var lifecycleTrace = args.Contains("--lifecycle", StringComparer.Ordinal)
    ? new LifecycleTrace()
    : null;
var useDefaultHistory = args.Contains("--default-history", StringComparer.Ordinal);
var approvalMode = args.Contains("--approval", StringComparer.Ordinal);

if (approvalMode)
{
    return await ApprovalProbeRunner.RunAsync(functions[0]) ? 0 : 5;
}

var options = new HarnessAgentOptions
{
    Name = "foundry-harness-compatibility-probe",
    HarnessInstructions = string.Empty,
    DisableAgentModeProvider = true,
    DisableAgentSkillsProvider = true,
    DisableFileMemory = true,
    DisableOpenTelemetry = true,
    DisableTodoProvider = true,
    DisableWebSearch = true,
    ChatOptions = new ChatOptions
    {
        Tools = [.. functions],
    },
};

if (lifecycleTrace is not null)
{
    if (!useDefaultHistory)
    {
        options.ChatHistoryProvider = new TraceChatHistoryProvider(lifecycleTrace);
    }
    options.AIContextProviders = [new TraceAIContextProvider(lifecycleTrace)];
    options.CompactionStrategy = new TraceCompactionStrategy(lifecycleTrace);
    options.MaximumIterationsPerRequest = 4;
}

var agent = new ProbeChatClient(functions[0].Name, lifecycleTrace).AsHarnessAgent(options);
AgentResponse response;

if (lifecycleTrace is not null)
{
    var injector = agent.GetService<MessageInjectingChatClient>();
    var functionInvokingChatClient = agent.GetService<FunctionInvokingChatClient>();
    if (injector is null ||
        functionInvokingChatClient is null ||
        agent.GetService<ToolApprovalAgent>() is null)
    {
        Console.Error.WriteLine("Harness middleware services were not discoverable.");
        return 2;
    }

    functionInvokingChatClient.FunctionInvoker = async (context, cancellationToken) =>
    {
        lifecycleTrace.Add($"function.invoker:{context.Function.Name}:{context.CallContent.CallId}");
        return await context.Function.InvokeAsync(context.Arguments, cancellationToken);
    };

    var session = await agent.CreateSessionAsync();
    await injector.EnqueueMessagesAsync(
        session,
        [new ChatMessage(ChatRole.User, "injected-probe")]);
    response = await agent.RunAsync(
        "Execute the generated probe tool.",
        session);
}
else
{
    response = await agent.RunAsync("Execute the generated probe tool.");
}

var responseText = response.GetText();

Console.WriteLine($"{agent.GetType().FullName}:{agent.Name}:{responseText}");
if (!string.Equals(responseText, "tool-result:aot", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Generated tool result did not round-trip through Harness.");
    return 3;
}

if (lifecycleTrace is not null)
{
    var actualEntries = lifecycleTrace.Entries;
    foreach (var entry in actualEntries)
    {
        Console.WriteLine($"TRACE:{entry}");
    }

    string[] expectedEntries = useDefaultHistory
        ?
        [
            "context.provide",
            "compaction:call=False:result=False:messages=2",
            "chat.call:1",
            "injected.seen",
            "context.seen",
            "context.store",
            "function.invoker:Echo:probe-call",
            "compaction:call=True:result=False:messages=3",
            "chat.call:2",
            "injected.seen",
            "context.seen",
            "context.store",
        ]
        :
        [
            "context.provide",
            "history.provide",
            "compaction:call=False:result=False:messages=3",
            "chat.call:1",
            "history.seen",
            "injected.seen",
            "context.seen",
            "history.store:2:1",
            "context.store",
            "function.invoker:Echo:probe-call",
            "history.provide",
            "chat.call:2",
            "history.seen",
            "context.seen",
            "history.store:1:1",
            "context.store",
        ];

    if (!actualEntries.SequenceEqual(expectedEntries, StringComparer.Ordinal))
    {
        Console.Error.WriteLine("Harness lifecycle trace was incomplete.");
        return 4;
    }
}

return 0;
