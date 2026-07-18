using System.Reflection;

using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.DevUI;

/// <summary>
/// Extension methods for bridging Foundry's <see cref="FoundryAgentAttribute"/>-declared
/// agents into MAF DevUI's entity discovery.
/// </summary>
/// <remarks>
/// <para>
/// MAF DevUI discovers agents via keyed <c>AIAgent</c> DI services. This bridge reads
/// the source-generated agent registry and uses MAF's <c>AddAIAgent</c> hosting API to
/// register each <see cref="FoundryAgentAttribute"/>-declared agent so DevUI's
/// <c>/v1/entities</c> endpoint lists them.
/// </para>
/// <para>
/// This package deliberately isolates the preview-only DevUI and Hosting package
/// dependencies from the stable <c>NexusLabs.Foundry.MicrosoftAgentFramework</c> package.
/// </para>
/// </remarks>
public static class FoundryDevUIServiceCollectionExtensions
{
    /// <summary>
    /// Registers all <see cref="FoundryAgentAttribute"/>-declared agents with MAF's
    /// hosting infrastructure so they appear in DevUI's entity discovery at
    /// <c>/v1/entities</c>.
    /// </summary>
    /// <param name="services">The service collection to register agents into.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The method reads agent types from the static
    /// <see cref="AgentFrameworkGeneratedBootstrap"/> registry (populated by the
    /// source-generated <c>[ModuleInitializer]</c>) and registers each using MAF's
    /// <c>AddAIAgent(name, instructions)</c> hosting API. The agent's
    /// <see cref="FoundryAgentAttribute.Instructions"/> and
    /// <see cref="FoundryAgentAttribute.Description"/> are read from the attribute.
    /// </para>
    /// <para>
    /// Agents are registered with instructions from the attribute. Their
    /// <c>IChatClient</c> is resolved from DI at runtime when DevUI invokes
    /// them — register an <c>IChatClient</c> in DI to enable interactive use.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    ///
    /// // Bridge Foundry agents -> DevUI
    /// builder.Services.AddFoundryDevUI();
    ///
    /// // MAF hosting + DevUI
    /// builder.Services.AddOpenAIResponses();
    /// builder.Services.AddOpenAIConversations();
    ///
    /// var app = builder.Build();
    /// app.MapOpenAIResponses();
    /// app.MapOpenAIConversations();
    /// app.MapDevUI();
    /// app.Run();
    /// </code>
    /// </example>
    public static IServiceCollection AddFoundryDevUI(
        this IServiceCollection services)
    {
        if (!AgentFrameworkGeneratedBootstrap.TryGetAgentTypes(out var agentTypesProvider))
        {
            return services;
        }

        var agentTypes = agentTypesProvider();

        foreach (var agentType in agentTypes)
        {
            var attribute = agentType.GetCustomAttribute<FoundryAgentAttribute>();
            if (attribute is null)
            {
                continue;
            }

            var agentName = agentType.Name;
            var agentFullName = agentType.FullName ?? agentName;

            services.AddAIAgent(agentName, (sp, key) =>
            {
                var factory = sp.GetService<IAgentFactory>();
                if (factory is not null)
                {
                    return factory.CreateAgent(agentFullName);
                }

                var chatClient = sp.GetRequiredService<Microsoft.Extensions.AI.IChatClient>();
                var instructions = attribute.Instructions ?? attribute.Description ?? $"Agent: {agentName}";
                return new Microsoft.Agents.AI.ChatClientAgent(
                    chatClient, agentName, instructions);
            });
        }

        return services;
    }
}
