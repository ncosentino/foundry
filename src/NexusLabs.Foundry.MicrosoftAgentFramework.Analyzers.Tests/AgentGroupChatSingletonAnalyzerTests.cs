using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class AgentGroupChatSingletonAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoError_WhenGroupHasTwoOrMoreMembers()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGroupChatMember(""code-review"")]
public class SecurityAgent { }

[FoundryAgent]
[AgentGroupChatMember(""code-review"")]
public class PerformanceAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGroupChatSingletonAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenGroupHasThreeMembers()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGroupChatMember(""code-review"")]
public class SecurityAgent { }

[FoundryAgent]
[AgentGroupChatMember(""code-review"")]
public class PerformanceAgent { }

[FoundryAgent]
[AgentGroupChatMember(""code-review"")]
public class StyleAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGroupChatSingletonAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenNoGroupChatMemberAttributes()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class SecurityAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGroupChatSingletonAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF002_WhenGroupHasOneMember()
    {
        // FDRYMAF002 is reported on the attribute application span
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[{|FDRYMAF002:AgentGroupChatMember(""code-review"")|}]
public class SecurityAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGroupChatSingletonAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF002_TwoGroupsOneUnderPopulated()
    {
        // code-review is fine (2 members), design-review has only 1 → error on design-review attribute
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGroupChatMember(""code-review"")]
public class SecurityAgent { }

[FoundryAgent]
[AgentGroupChatMember(""code-review"")]
public class PerformanceAgent { }

[FoundryAgent]
[{|FDRYMAF002:AgentGroupChatMember(""design-review"")|}]
public class DesignAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGroupChatSingletonAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
