using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class AgentOrphanAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoInfo_WhenAgentIsHandoffSource()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentHandoffsTo(typeof(ExpertAgent))]
public class TriageAgent { }

[FoundryAgent]
public class ExpertAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoInfo_WhenAgentIsHandoffTarget()
    {
        // ExpertAgent is referenced as a handoff target — should not be flagged
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentHandoffsTo(typeof(ExpertAgent))]
public class TriageAgent { }

[FoundryAgent]
public class ExpertAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoInfo_WhenAgentIsGroupChatMember()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGroupChatMember(""review"")]
public class SecurityAgent { }

[FoundryAgent]
[AgentGroupChatMember(""review"")]
public class PerformanceAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoInfo_WhenAgentIsSequenceMember()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentSequenceMember(""pipeline"", 0)]
public class WriterAgent { }

[FoundryAgent]
[AgentSequenceMember(""pipeline"", 1)]
public class EditorAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Info_FDRYMAF008_WhenAgentHasNoTopologyDeclaration()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[{|FDRYMAF008:FoundryAgent|}]
public class OrphanAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Info_FDRYMAF008_ForEachOrphanAgent()
    {
        // Two orphan agents → two diagnostics
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[{|FDRYMAF008:FoundryAgent|}]
public class OrphanA { }

[{|FDRYMAF008:FoundryAgent|}]
public class OrphanB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Info_FDRYMAF008_OnlyForOrphan_NotForTopologyParticipants()
    {
        // TriageAgent and ExpertAgent participate in handoff; StandaloneAgent does not
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentHandoffsTo(typeof(ExpertAgent))]
public class TriageAgent { }

[FoundryAgent]
public class ExpertAgent { }

[{|FDRYMAF008:FoundryAgent|}]
public class StandaloneAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoInfo_WhenNoAgentsExist()
    {
        var code = @"
public class SomeClass { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoInfo_WhenAgentHasOnlyGraphEdgeAttribute()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
public class AgentA { }

[FoundryAgent]
public class AgentB { }
" + Attributes + MafTestAttributes.GraphAttributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoInfo_WhenAgentHasOnlyGraphEntryAttribute()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEntry(""Pipeline"")]
public class AgentA { }
" + Attributes + MafTestAttributes.GraphAttributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoInfo_WhenAgentHasOnlyGraphNodeAttribute()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphNode(""Pipeline"")]
public class AgentA { }
" + Attributes + MafTestAttributes.GraphAttributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoInfo_WhenAgentHasOnlyGraphReducerAttribute()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphReducer(""Pipeline"")]
public class AgentA { }
" + Attributes + MafTestAttributes.GraphAttributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
