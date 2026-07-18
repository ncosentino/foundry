using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class AgentGraphReachabilityAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All + MafTestAttributes.GraphAttributes;

    [Fact]
    public async Task NoDiagnostic_WhenAllNodesReachable()
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

        var test = new CSharpAnalyzerTest<AgentGraphReachabilityAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_FDRYMAF022_WhenNodeUnreachable()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEntry(""Pipeline"")]
[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
public class AgentA { }

[FoundryAgent]
public class AgentB { }

[FoundryAgent]
[{|FDRYMAF022:AgentGraphEdge(""Pipeline"", typeof(AgentB))|}]
public class Disconnected { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphReachabilityAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNoEntryPoint()
    {
        // Without an entry point, reachability cannot be assessed — other analyzers handle that
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
public class AgentA { }

[FoundryAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphReachabilityAnalyzer, DefaultVerifier>
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

        var test = new CSharpAnalyzerTest<AgentGraphReachabilityAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
