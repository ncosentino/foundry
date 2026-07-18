using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class AgentGraphTerminalNodeAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All + MafTestAttributes.GraphAttributes;

    [Fact]
    public async Task NoDiagnostic_WhenTerminalNodeHasNoEdges()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphNode(""Pipeline"", IsTerminal = true)]
public class TerminalAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTerminalNodeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF027_WhenTerminalNodeHasOutgoingEdges()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[{|FDRYMAF027:AgentGraphNode(""Pipeline"", IsTerminal = true)|}]
[AgentGraphEdge(""Pipeline"", typeof(NextAgent))]
public class TerminalAgent { }

[FoundryAgent]
public class NextAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTerminalNodeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNodeIsNotTerminal()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphNode(""Pipeline"")]
[AgentGraphEdge(""Pipeline"", typeof(NextAgent))]
public class AgentA { }

[FoundryAgent]
public class NextAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTerminalNodeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenTerminalInDifferentGraph()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphNode(""Graph1"", IsTerminal = true)]
[AgentGraphEdge(""Graph2"", typeof(NextAgent))]
public class AgentA { }

[FoundryAgent]
public class NextAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTerminalNodeAnalyzer, DefaultVerifier>
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
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTerminalNodeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
