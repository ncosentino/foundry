using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class AgentGraphTopologyAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All + MafTestAttributes.GraphAttributes;

    [Fact]
    public async Task NoDiagnostic_WhenSourceAndTargetAreAgents()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
public class AgentA { }

[FoundryAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF019_WhenTargetLacksFoundryAgent()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[{|FDRYMAF019:AgentGraphEdge(""Pipeline"", typeof(NotAnAgent))|}]
public class AgentA { }

public class NotAnAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_FDRYMAF020_WhenSourceLacksFoundryAgent()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
public class {|FDRYMAF020:NotAnAgent|} { }

[FoundryAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_FDRYMAF021_WhenEntryPointLacksFoundryAgent()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[AgentGraphEntry(""Pipeline"")]
public class {|FDRYMAF021:NotAnAgent|} { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenEntryPointHasFoundryAgent()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEntry(""Pipeline"")]
public class AgentA { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task BothDiagnostics_WhenSourceAndTargetBothLackFoundryAgent()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[{|FDRYMAF019:AgentGraphEdge(""Pipeline"", typeof(AlsoNotAnAgent))|}]
public class {|FDRYMAF020:NotAnAgent|} { }

public class AlsoNotAnAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNoGraphAttributes()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class AgentA { }

public class PlainClass { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
