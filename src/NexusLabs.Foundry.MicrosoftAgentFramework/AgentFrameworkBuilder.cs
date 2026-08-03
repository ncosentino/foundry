using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NexusLabs.Foundry.MicrosoftAgentFramework;

/// <summary>
/// Fluent builder for configuring the Microsoft Agent Framework with Foundry function discovery.
/// </summary>
/// <remarks>
/// <para>
/// When the Foundry source generator is active (the common case), this class uses pre-built
/// <see cref="IAIFunctionProvider"/> instances registered by the generated <c>[ModuleInitializer]</c>.
/// No reflection is required in that path.
/// </para>
/// <para>
/// When the source generator is not used, this class falls back to reflection to discover
/// methods decorated with <see cref="AgentFunctionAttribute"/>. That path carries
/// <c>[RequiresDynamicCode]</c> and is not NativeAOT-compatible.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Obtained from SyringeAgentFrameworkExtensions.UsingAgentFramework()
/// AgentFrameworkBuilder syringe = app.Services.UsingAgentFramework();
///
/// // Register function types and build the factory
/// IAgentFactory factory = syringe
///     .AddAgentFunctionsFromGenerated(GeneratedAgentFunctions.AllFunctionTypes)
///     .BuildAgentFactory();
///
/// // Create agents from the factory
/// var supportAgent = factory.CreateAgent&lt;CustomerSupportAgent&gt;();
/// </code>
/// </example>
public sealed record AgentFrameworkBuilder
{
    public required IServiceProvider ServiceProvider { get; init; }

    internal List<Action<AgentFrameworkConfigureOptions>>? ConfigureAgentFactory { get; init; } = [];

    internal List<Type>? FunctionTypes { get; init; } = [];

    internal IReadOnlyDictionary<string, IReadOnlyList<Type>>? FunctionGroupMap { get; init; }

    internal List<Type>? AgentTypes { get; init; } = [];

    /// <summary>
    /// Agent-builder plugins applied to every agent created by the factory.
    /// Populated by middleware extension methods such as <c>UsingToolResultMiddleware()</c>
    /// and <c>UsingResilience()</c>.
    /// </summary>
    internal IReadOnlyList<IAIAgentBuilderPlugin>? Plugins { get; init; }

    /// <summary>
    /// Factory that creates an <see cref="IAIAgentBuilderPlugin"/> from a
    /// <see cref="AgentResilienceAttribute"/> found on an agent type.
    /// Set by <c>UsingResilience()</c> to enable per-agent resilience overrides via
    /// <c>[AgentResilience]</c>.
    /// </summary>
    internal Func<AgentResilienceAttribute, IAIAgentBuilderPlugin>? PerAgentResilienceFactory { get; init; }

    /// <summary>
    /// Metrics configuration (meter name, ActivitySource name). Populated by
    /// <c>ConfigureMetrics()</c>.
    /// </summary>
    internal Diagnostics.AgentFrameworkMetricsOptions? MetricsOptions { get; init; }

    /// <summary>
    /// Pipeline-shape metrics configuration (meter name, ActivitySource name).
    /// Populated by <c>ConfigurePipelineMetrics()</c>. When <see langword="null"/>
    /// the registered <see cref="Diagnostics.IPipelineMetrics"/> is the no-op
    /// implementation; observability is opt-in with zero overhead by default.
    /// </summary>
    internal Diagnostics.PipelineMetricsOptions? PipelineMetricsOptions { get; init; }

    /// <summary>
    /// Whether <c>UsingTokenTracking()</c> has already been called. Prevents
    /// double-wiring the recording middleware when both <c>UsingTokenBudget()</c>
    /// and <c>UsingDiagnostics()</c> are used together.
    /// </summary>
    internal bool TokenTrackingWired { get; init; }

    public IAgentFactory BuildAgentFactory()
    {
        var groupTypes = (FunctionGroupMap ?? new Dictionary<string, IReadOnlyList<Type>>())
            .SelectMany(kvp => kvp.Value);

        var allFunctionTypes = (FunctionTypes ?? [])
            .Concat(groupTypes)
            .Distinct()
            .ToList();

        var agentTypeMap = BuildAgentTypeMap(AgentTypes ?? []);

        AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(out var generatedProvider);

        return new AgentFactory(
            serviceProvider: ServiceProvider,
            configureCallbacks: ConfigureAgentFactory ?? [],
            functionTypes: allFunctionTypes,
            functionGroupMap: FunctionGroupMap,
            agentTypeMap: agentTypeMap,
            generatedProvider: generatedProvider,
            plugins: Plugins ?? [],
            perAgentResilienceFactory: PerAgentResilienceFactory);
    }

    /// <summary>
    /// Maps every registered agent type by its fully-qualified name, and additionally by the name it
    /// is published under so a caller can address an agent without repeating its namespace.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fully-qualified name is always registered, because it is the only name guaranteed to be
    /// unique and it is what a caller must fall back to.
    /// </para>
    /// <para>
    /// The published name comes from <see cref="FoundryAgentName"/>, so declaring
    /// <c>[FoundryAgent(Name = "…")]</c> changes what this map is keyed on. The class name is not
    /// also kept as an alias in that case: the point of declaring a name is that exactly one name is
    /// published, and keeping the class name addressable alongside it would leave the class rename
    /// the declaration was meant to survive still able to break a caller.
    /// </para>
    /// <para>
    /// A published name shared by two agents is rejected rather than silently resolving to whichever
    /// registered first, or being quietly dropped in favour of fully-qualified lookup. Both of those
    /// would let a workflow document or a configuration string keep working while addressing an
    /// agent nobody intended. Failing here surfaces the collision at composition, where the fix is
    /// to rename one of them.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Two agent types share a fully-qualified name, or two share a published name.
    /// </exception>
    private static Dictionary<string, Type> BuildAgentTypeMap(IReadOnlyList<Type> agentTypes)
    {
        var agentTypeMap = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var agentType in agentTypes)
        {
            var fullName = agentType.FullName ?? agentType.Name;
            if (!agentTypeMap.TryAdd(fullName, agentType))
            {
                throw new InvalidOperationException(
                    $"Duplicate agent registration: '{fullName}' is already registered as " +
                    $"'{agentTypeMap[fullName].AssemblyQualifiedName}'. Cannot also register " +
                    $"'{agentType.AssemblyQualifiedName}'. Ensure each [FoundryAgent] class has a unique fully-qualified name.");
            }
        }

        var publishedNameGroups = agentTypes.GroupBy(FoundryAgentName.Resolve, StringComparer.Ordinal);

        foreach (var group in publishedNameGroups)
        {
            var candidates = group.ToList();
            if (candidates.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Ambiguous agent name '{group.Key}': it is published by " +
                    $"{string.Join(" and ", candidates.Select(DescribeNameSource))}. " +
                    "Agents are addressable by their published name, so each [FoundryAgent] class " +
                    "must publish a unique name even across namespaces. Rename one of them, or give " +
                    "one a distinct [FoundryAgent(Name = \"…\")].");
            }

            // A published name identical to some other agent's full name would otherwise overwrite
            // it, so the unambiguous full-name mapping wins and the alias is skipped.
            agentTypeMap.TryAdd(group.Key, candidates[0]);
        }

        return agentTypeMap;
    }

    /// <summary>
    /// Describes where an agent's published name came from, so a collision message distinguishes a
    /// declared name from one derived from the class name and names the file to edit either way.
    /// </summary>
    private static string DescribeNameSource(Type agentType)
    {
        var declared = agentType.GetCustomAttribute<FoundryAgentAttribute>()?.Name;
        return string.IsNullOrWhiteSpace(declared)
            ? $"'{agentType.FullName}' (from its class name)"
            : $"'{agentType.FullName}' (declared as [FoundryAgent(Name = \"{declared}\")])";
    }
}
