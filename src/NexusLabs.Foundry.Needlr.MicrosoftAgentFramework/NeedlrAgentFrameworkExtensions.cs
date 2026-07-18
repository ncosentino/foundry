using System.Diagnostics.CodeAnalysis;

using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Needlr.Injection;

namespace NexusLabs.Foundry.Needlr.MicrosoftAgentFramework;

/// <summary>
/// Adds Foundry's Microsoft Agent Framework runtime to a Needlr composition.
/// </summary>
public static class NeedlrAgentFrameworkExtensions
{
    /// <summary>
    /// Adds agent functions discovered from Needlr's registered service descriptors.
    /// </summary>
    /// <param name="builder">The Foundry runtime builder.</param>
    /// <returns>The updated builder.</returns>
    [RequiresUnreferencedCode(
        "Service provider scanning uses reflection to discover types with AgentFunction attributes.")]
    [RequiresDynamicCode(
        "Service provider scanning uses reflection APIs that require dynamic code generation.")]
    public static AgentFrameworkBuilder AddAgentFunctionsFromProvider(
        this AgentFrameworkBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var scanner = new ServiceProviderAgentFunctionScanner(builder.ServiceProvider);
        return builder.AddAgentFunctionsFromScanner(scanner);
    }

    /// <summary>
    /// Registers the Foundry runtime with default configuration.
    /// </summary>
    /// <param name="syringe">The Needlr composition to configure.</param>
    /// <returns>The updated Needlr composition.</returns>
    public static ConfiguredSyringe UsingAgentFramework(
        this ConfiguredSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingAgentFramework(static builder => builder);
    }

    /// <summary>
    /// Registers the Foundry runtime with explicit builder configuration.
    /// </summary>
    /// <param name="syringe">The Needlr composition to configure.</param>
    /// <param name="configure">The Foundry runtime builder configuration.</param>
    /// <returns>The updated Needlr composition.</returns>
    public static ConfiguredSyringe UsingAgentFramework(
        this ConfiguredSyringe syringe,
        Func<AgentFrameworkBuilder, AgentFrameworkBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);
        return syringe.UsingPostPluginRegistrationCallback(
            services => services.AddFoundryAgentFramework(configure));
    }

    /// <summary>
    /// Registers the Foundry runtime using a caller-created builder.
    /// </summary>
    /// <param name="syringe">The Needlr composition to configure.</param>
    /// <param name="configure">A function that creates the configured builder.</param>
    /// <returns>The updated Needlr composition.</returns>
    public static ConfiguredSyringe UsingAgentFramework(
        this ConfiguredSyringe syringe,
        Func<AgentFrameworkBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);
        return syringe.UsingAgentFramework(_ => configure());
    }
}
