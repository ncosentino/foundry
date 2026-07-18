using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class AgentGraphCycleAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All + MafTestAttributes.GraphAttributes;

    [Fact]
    public async Task NoDiagnostic_WhenNoCycleExists()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEntry(""Pipeline"")]
[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
public class AgentA { }

[FoundryAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentC))]
public class AgentB { }

[FoundryAgent]
public class AgentC { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphCycleAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNoGraphEdges()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class AgentA { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphCycleAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF016_TwoNodeCycle()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[{|FDRYMAF016:AgentGraphEdge(""Pipeline"", typeof(AgentB))|}]
public class AgentA { }

[FoundryAgent]
[{|FDRYMAF016:AgentGraphEdge(""Pipeline"", typeof(AgentA))|}]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphCycleAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF016_ThreeNodeCycle()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[{|FDRYMAF016:AgentGraphEdge(""Pipeline"", typeof(AgentB))|}]
public class AgentA { }

[FoundryAgent]
[{|FDRYMAF016:AgentGraphEdge(""Pipeline"", typeof(AgentC))|}]
public class AgentB { }

[FoundryAgent]
[{|FDRYMAF016:AgentGraphEdge(""Pipeline"", typeof(AgentA))|}]
public class AgentC { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphCycleAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_CycleInDifferentGraph()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEdge(""Graph1"", typeof(AgentB))]
public class AgentA { }

[FoundryAgent]
[AgentGraphEdge(""Graph2"", typeof(AgentA))]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphCycleAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF016_SelfLoop()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[{|FDRYMAF016:AgentGraphEdge(""Pipeline"", typeof(SelfLoopAgent))|}]
public class SelfLoopAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphCycleAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_DiamondDag()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
[AgentGraphEdge(""Pipeline"", typeof(AgentC))]
public class AgentA { }

[FoundryAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentD))]
public class AgentB { }

[FoundryAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentD))]
public class AgentC { }

[FoundryAgent]
public class AgentD { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphCycleAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
