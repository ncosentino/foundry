using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class AgentCyclicHandoffAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoWarning_WhenNoCycleExists()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class GeographyAgent { }

[FoundryAgent]
[AgentHandoffsTo(typeof(GeographyAgent))]
public class TriageAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentCyclicHandoffAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenNoHandoffAttributes()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class GeographyAgent { }

[FoundryAgent]
public class TriageAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentCyclicHandoffAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_LinearChain()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class AgentC { }

[FoundryAgent]
[AgentHandoffsTo(typeof(AgentC))]
public class AgentB { }

[FoundryAgent]
[AgentHandoffsTo(typeof(AgentB))]
public class AgentA { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentCyclicHandoffAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_FDRYMAF004_TwoNodeCycle()
    {
        // FDRYMAF004 is reported on each attribute application in the cycle
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[{|FDRYMAF004:AgentHandoffsTo(typeof(AgentB))|}]
public class AgentA { }

[FoundryAgent]
[{|FDRYMAF004:AgentHandoffsTo(typeof(AgentA))|}]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentCyclicHandoffAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_FDRYMAF004_ThreeNodeCycle()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[{|FDRYMAF004:AgentHandoffsTo(typeof(AgentB))|}]
public class AgentA { }

[FoundryAgent]
[{|FDRYMAF004:AgentHandoffsTo(typeof(AgentC))|}]
public class AgentB { }

[FoundryAgent]
[{|FDRYMAF004:AgentHandoffsTo(typeof(AgentA))|}]
public class AgentC { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentCyclicHandoffAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
