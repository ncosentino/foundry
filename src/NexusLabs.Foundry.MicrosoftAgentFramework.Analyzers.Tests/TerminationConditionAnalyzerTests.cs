using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class TerminationConditionAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    // ─── FDRYMAF009 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task NoWarning_009_WhenWorkflowRunConditionOnAgent()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentSequenceMember(""pipeline"", 0)]
[WorkflowRunTerminationCondition(typeof(MyCondition), ""DONE"")]
public class WriterAgent { }

public class MyCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_FDRYMAF009_WhenWorkflowRunConditionOnNonAgent()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[{|FDRYMAF009:WorkflowRunTerminationCondition(typeof(MyCondition), ""DONE"")|}]
public class NotAnAgent { }

public class MyCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_FDRYMAF009_MultipleConditions_AllFlagNonAgent()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[{|FDRYMAF009:WorkflowRunTerminationCondition(typeof(MyCondition), ""A"")|}]
[{|FDRYMAF009:WorkflowRunTerminationCondition(typeof(MyCondition), ""B"")|}]
public class NotAnAgent { }

public class MyCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    // ─── FDRYMAF010 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task NoError_010_WhenWorkflowRunConditionTypeImplementsInterface()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentSequenceMember(""pipeline"", 0)]
[WorkflowRunTerminationCondition(typeof(ValidCondition), ""DONE"")]
public class WriterAgent { }

public class ValidCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF010_WhenWorkflowRunConditionTypeDoesNotImplementInterface()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentSequenceMember(""pipeline"", 0)]
[{|FDRYMAF010:WorkflowRunTerminationCondition(typeof(NotACondition), ""DONE"")|}]
public class WriterAgent { }

public class NotACondition { }
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_010_WhenAgentTerminationConditionTypeImplementsInterface()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGroupChatMember(""review"")]
[AgentTerminationCondition(typeof(ValidCondition), ""APPROVED"")]
public class ReviewerAgent { }

[FoundryAgent]
[AgentGroupChatMember(""review"")]
public class WriterAgent { }

public class ValidCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF010_WhenAgentTerminationConditionTypeDoesNotImplementInterface()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGroupChatMember(""review"")]
[{|FDRYMAF010:AgentTerminationCondition(typeof(NotACondition), ""APPROVED"")|}]
public class ReviewerAgent { }

[FoundryAgent]
[AgentGroupChatMember(""review"")]
public class WriterAgent { }

public class NotACondition { }
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF010_MultipleConditions_EachBadTypeFlagged()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentSequenceMember(""pipeline"", 0)]
[{|FDRYMAF010:WorkflowRunTerminationCondition(typeof(BadTypeA), ""X"")|}]
[{|FDRYMAF010:WorkflowRunTerminationCondition(typeof(BadTypeB), ""Y"")|}]
public class WriterAgent { }

public class BadTypeA { }
public class BadTypeB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    // ─── FDRYMAF011 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task NoInfo_011_WhenWorkflowRunConditionOnSequenceMember()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentSequenceMember(""pipeline"", 0)]
[WorkflowRunTerminationCondition(typeof(MyCondition), ""DONE"")]
public class WriterAgent { }

[FoundryAgent]
[AgentSequenceMember(""pipeline"", 1)]
public class EditorAgent { }

public class MyCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Info_FDRYMAF011_WhenWorkflowRunConditionOnGroupChatMember()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGroupChatMember(""review"")]
[{|FDRYMAF011:WorkflowRunTerminationCondition(typeof(MyCondition), ""APPROVED"")|}]
public class ReviewerAgent { }

[FoundryAgent]
[AgentGroupChatMember(""review"")]
public class WriterAgent { }

public class MyCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoInfo_011_WhenAgentTerminationConditionOnGroupChatMember()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGroupChatMember(""review"")]
[AgentTerminationCondition(typeof(MyCondition), ""APPROVED"")]
public class ReviewerAgent { }

[FoundryAgent]
[AgentGroupChatMember(""review"")]
public class WriterAgent { }

public class MyCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Info_FDRYMAF011_MultipleConditions_EachFlagged()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGroupChatMember(""review"")]
[{|FDRYMAF011:WorkflowRunTerminationCondition(typeof(MyCondition), ""A"")|}]
[{|FDRYMAF011:WorkflowRunTerminationCondition(typeof(MyCondition), ""B"")|}]
public class ReviewerAgent { }

[FoundryAgent]
[AgentGroupChatMember(""review"")]
public class WriterAgent { }

public class MyCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    // ─── combinations ────────────────────────────────────────────────────────

    [Fact]
    public async Task MultipleIssues_009And010_WhenBothApply()
    {
        // Not an agent AND condition type is wrong → both 009 and 010 fire on the same attribute
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[WorkflowRunTerminationCondition(typeof(BadType), ""X"")]
public class NotAnAgent { }

public class BadType { }
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                DiagnosticResult.CompilerError("FDRYMAF010").WithSpan(4, 2, 4, 55).WithArguments("BadType", "NotAnAgent"),
                DiagnosticResult.CompilerWarning("FDRYMAF009").WithSpan(4, 2, 4, 55).WithArguments("NotAnAgent"),
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
