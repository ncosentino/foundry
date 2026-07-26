using System.Collections.Concurrent;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAgentExecutionContextAccessor _contextAccessor;

    /// <summary>
    /// Initializes a Harness scenario runner.
    /// </summary>
    /// <param name="services">Services used to activate generated function groups.</param>
    /// <param name="contextAccessor">The trusted execution-context accessor scoped around the run.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="contextAccessor"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="services"/> does not expose an <see cref="IServiceScopeFactory"/>.
    /// </exception>
    public HarnessScenarioRunner(
        IServiceProvider services,
        IAgentExecutionContextAccessor contextAccessor)
    {
        ArgumentNullException.ThrowIfNull(services);
        _scopeFactory = services.GetService<IServiceScopeFactory>()
            ?? throw new InvalidOperationException(
                "HarnessScenarioRunner requires an IServiceScopeFactory so every run can " +
                "resolve generated tools and agent dependencies in an isolated scope.");
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
    /// <remarks>
    /// Construction, session, execution, and cancellation exceptions are captured in
    /// <see cref="HarnessScenarioRunResult.ExecutionError"/> so verification receives the same
    /// failure evidence as the existing <see cref="AgentScenarioRunner"/>.
    /// </remarks>
    public async Task<HarnessScenarioRunResult> RunAsync(
        IHarnessScenario scenario,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario.Name);
        ArgumentNullException.ThrowIfNull(scenario.GeneratedFunctionTypes);

        using var serviceScope = _scopeFactory.CreateScope();
        var runServices = serviceScope.ServiceProvider;
        var generatedTools = ResolveGeneratedTools(
            scenario.GeneratedFunctionTypes,
            runServices);
        var resolvedToolNames = generatedTools
            .Select(function => function.Name)
            .ToArray();
        var executedToolNames = new ConcurrentQueue<string>();
        var trackingTools = generatedTools
            .Select(function => new HarnessScenarioTrackingAIFunction(
                function,
                executedToolNames))
            .Cast<AIFunction>()
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
                    runServices,
                    workspace,
                    trackingTools));
                if (agent is null)
                {
                    throw new InvalidOperationException(
                        $"Harness scenario '{scenario.Name}' returned a null agent.");
                }

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
        IReadOnlyList<Type> functionTypes,
        IServiceProvider resolutionServices)
    {
        if (!AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(out var provider))
        {
            throw new HarnessScenarioToolResolutionException(
                "No source-generated AIFunction provider is registered.",
                generatedProviderUnavailable: true,
                missingFunctionTypes: [],
                duplicateToolNames: []);
        }

        for (int index = 0; index < functionTypes.Count; index++)
        {
            if (functionTypes[index] is null)
            {
                throw new ArgumentException(
                    $"GeneratedFunctionTypes contains a null entry at index {index}.",
                    nameof(functionTypes));
            }
        }

        var distinctFunctionTypes = functionTypes
            .Distinct()
            .ToArray();
        var functions = new List<AIFunction>();
        var missingTypes = new List<Type>();
        for (int index = 0; index < distinctFunctionTypes.Length; index++)
        {
            var functionType = distinctFunctionTypes[index];
            if (!provider.TryGetFunctions(
                functionType,
                resolutionServices,
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
}
