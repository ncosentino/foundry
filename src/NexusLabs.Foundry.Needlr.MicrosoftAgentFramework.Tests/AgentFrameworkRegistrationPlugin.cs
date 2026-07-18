using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Needlr;

namespace NexusLabs.Foundry.Needlr.MicrosoftAgentFramework.Tests;

/// <summary>
/// Registers Foundry through Needlr's generated plugin pipeline.
/// </summary>
internal sealed class AgentFrameworkRegistrationPlugin :
    IServiceCollectionPlugin
{
    internal const string ConfiguredMeterName =
        "Foundry.Tests.PluginConfigured.Agents";

    /// <inheritdoc />
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddFoundryAgentFramework(builder => builder
            .ConfigureMetrics(metrics =>
                metrics.MeterName = ConfiguredMeterName));
    }
}
