using System.Collections.Concurrent;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Testing;

/// <summary>
/// Runs <see cref="IHarnessScenario"/> instances through a deterministic generated-tool,
/// workspace, execution-context, session, and verification lifecycle.
/// </summary>
/// <remarks>
/// Agent construction remains scenario-owned so this package does not reference the optional
/// complete-Harness package. Generated functions are resolved exclusively through
/// <see cref="AgentFrameworkGeneratedBootstrap"/>; no reflection fallback is available.
/// </remarks>
public sealed class HarnessScenarioRunner
{
    private readonly IServiceProvider _services;
    private readonly IAgentExecutionContextAccessor _contextAccessor;

    /// <summary>
    /// Initializes a Harness scenario runner.
    /// </summary>
    /// <param name="services">Services used to activate generated function groups.</param>
    /// <param name="contextAccessor">The trusted execution-context accessor scoped around the run.</param>
    public HarnessScenarioRunner(
        IServiceProvider services,
        IAgentExecutionContextAccessor contextAccessor)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
    }

    /// <summary>
    /// Runs a scenario without cancellation.
    /// </summary>
    /// <param name="scenario">The scenario to run.</param>
    /// <returns>The completed scenario result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scenario"/> is null.</exception>
    /// <exception cref="HarnessScenarioToolResolutionException">
    /// Generated functions are unavailable, missing, or duplicated.
    /// </exception>
    public Task<HarnessScenarioRunResult> RunAsync(
        IHarnessScenario scenario) =>
        RunAsync(scenario, CancellationToken.None);

    /// <summary>
    /// Runs a scenario with cancellation.
    /// </summary>
    /// <param name="scenario">The scenario to run.</param>
    /// <param name="cancellationToken">Cancels agent construction or execution.</param>
    /// <returns>The completed scenario result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scenario"/> is null.</exception>
    /// <exception cref="HarnessScenarioToolResolutionException">
    /// Generated functions are unavailable, missing, or duplicated.
    /// </exception>
    public async Task<HarnessScenarioRunResult> RunAsync(
        IHarnessScenario scenario,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario.Name);
        ArgumentNullException.ThrowIfNull(scenario.GeneratedFunctionTypes);

        var generatedTools = ResolveGeneratedTools(scenario.GeneratedFunctionTypes);
        var resolvedToolNames = generatedTools
            .Select(function => function.Name)
            .ToArray();
        var workspace = new InMemoryWorkspace();
        scenario.SeedWorkspace(workspace);

        string runId = Guid.NewGuid().ToString("N");
        string userId = "harness-scenario-runner";
        string orchestrationId = $"harness-scenario-{scenario.Name}-{runId}";
        string sessionId = $"harness-session-{scenario.Name}-{runId}";
        var executionContext = new AgentExecutionContext(
            UserId: userId,
            OrchestrationId: orchestrationId,
            Properties: null,
            Workspace: workspace);

        AgentSession? session = null;
        string? responseText = null;
        Exception? executionError = null;
        var executedToolNames = new ConcurrentQueue<string>();
        AIAgent? agent = null;

        using (_contextAccessor.BeginScope(executionContext))
        {
            try
            {
                agent = scenario.CreateAgent(new HarnessScenarioAgentContext(
                    scenario.Name,
                    userId,
                    orchestrationId,
                    sessionId,
                    _services,
                    workspace,
                    generatedTools));
                if (agent is null)
                {
                    throw new InvalidOperationException(
                        $"Harness scenario '{scenario.Name}' returned a null agent.");
                }

                ChainToolExecutionCapture(agent, executedToolNames);
                session = await agent
                    .CreateSessionAsync(cancellationToken)
                    .ConfigureAwait(false);
                var response = await agent
                    .RunAsync(
                        scenario.UserPrompt,
                        session,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                responseText = response.GetText();
            }
            catch (Exception ex)
            {
                executionError = ex;
            }
            finally
            {
                (agent as IDisposable)?.Dispose();
            }
        }

        var executedNames = executedToolNames.ToArray();
        Exception? verificationError = null;
        try
        {
            scenario.Verify(workspace, diagnostics: null);
        }
        catch (Exception ex)
        {
            verificationError = ex;
        }

        var verificationContext = new HarnessScenarioVerificationContext(
            scenario.Name,
            userId,
            orchestrationId,
            sessionId,
            workspace,
            session,
            responseText,
            resolvedToolNames,
            executedNames,
            executionError);
        Exception? harnessVerificationError = null;
        try
        {
            scenario.VerifyHarness(verificationContext);
        }
        catch (Exception ex)
        {
            harnessVerificationError = ex;
        }

        return new HarnessScenarioRunResult(
            scenario.Name,
            userId,
            orchestrationId,
            sessionId,
            workspace,
            session,
            responseText,
            resolvedToolNames,
            executedNames,
            executionError,
            verificationError,
            harnessVerificationError,
            Succeeded:
                executionError is null &&
                verificationError is null &&
                harnessVerificationError is null);
    }

    private IReadOnlyList<AIFunction> ResolveGeneratedTools(
        IReadOnlyList<Type> functionTypes)
    {
        if (!AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(out var provider))
        {
            throw new HarnessScenarioToolResolutionException(
                "No source-generated AIFunction provider is registered.",
                generatedProviderUnavailable: true,
                missingFunctionTypes: [],
                duplicateToolNames: []);
        }

        var functions = new List<AIFunction>();
        var missingTypes = new List<Type>();
        for (int index = 0; index < functionTypes.Count; index++)
        {
            var functionType = functionTypes[index];
            if (functionType is null)
            {
                throw new ArgumentException(
                    $"GeneratedFunctionTypes contains a null entry at index {index}.",
                    nameof(functionTypes));
            }

            if (!provider.TryGetFunctions(
                functionType,
                _services,
                out var resolvedFunctions))
            {
                missingTypes.Add(functionType);
                continue;
            }

            functions.AddRange(resolvedFunctions);
        }

        if (missingTypes.Count > 0)
        {
            throw new HarnessScenarioToolResolutionException(
                "One or more declared function-group types were absent from the source-generated provider.",
                generatedProviderUnavailable: false,
                missingFunctionTypes: missingTypes.AsReadOnly(),
                duplicateToolNames: []);
        }

        var duplicateNames = functions
            .GroupBy(function => function.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            throw new HarnessScenarioToolResolutionException(
                "Declared function groups produced duplicate tool names.",
                generatedProviderUnavailable: false,
                missingFunctionTypes: [],
                duplicateToolNames: duplicateNames.AsReadOnly());
        }

        return functions.AsReadOnly();
    }

    private static void ChainToolExecutionCapture(
        AIAgent agent,
        ConcurrentQueue<string> executedToolNames)
    {
        var functionInvokingChatClient = agent.GetService<FunctionInvokingChatClient>();
        if (functionInvokingChatClient is null)
        {
            throw new InvalidOperationException(
                "The scenario agent did not expose a FunctionInvokingChatClient. " +
                "Harness tool execution cannot be observed without taking loop ownership.");
        }

        var existingFunctionInvoker = functionInvokingChatClient.FunctionInvoker;
        functionInvokingChatClient.FunctionInvoker = async (context, cancellationToken) =>
        {
            executedToolNames.Enqueue(context.Function.Name);
            return existingFunctionInvoker is null
                ? await context.Function
                    .InvokeAsync(context.Arguments, cancellationToken)
                    .ConfigureAwait(false)
                : await existingFunctionInvoker(context, cancellationToken)
                    .ConfigureAwait(false);
        };
    }
}
