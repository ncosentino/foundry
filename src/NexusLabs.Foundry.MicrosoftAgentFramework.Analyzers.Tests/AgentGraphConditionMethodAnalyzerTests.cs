using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class AgentGraphConditionMethodAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All + MafTestAttributes.GraphAttributes;

    [Fact]
    public async Task NoDiagnostic_WhenConditionMethodExists()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEntry(""Pipeline"")]
[AgentGraphEdge(""Pipeline"", typeof(AgentB), Condition = ""ShouldRoute"")]
public class AgentA
{
    public static bool ShouldRoute(object? input) => true;
}

[FoundryAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphConditionMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNoCondition()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEntry(""Pipeline"")]
[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
public class AgentA { }

[FoundryAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphConditionMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF028_WhenConditionMethodNotFound()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEntry(""Pipeline"")]
[{|FDRYMAF028:AgentGraphEdge(""Pipeline"", typeof(AgentB), Condition = ""NonexistentMethod"")|}]
public class AgentA { }

[FoundryAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphConditionMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF028_WhenConditionMethodNotStatic()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEntry(""Pipeline"")]
[{|FDRYMAF028:AgentGraphEdge(""Pipeline"", typeof(AgentB), Condition = ""ShouldRoute"")|}]
public class AgentA
{
    public bool ShouldRoute(object? input) => true;
}

[FoundryAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphConditionMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF028_WhenConditionMethodWrongReturnType()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEntry(""Pipeline"")]
[{|FDRYMAF028:AgentGraphEdge(""Pipeline"", typeof(AgentB), Condition = ""ShouldRoute"")|}]
public class AgentA
{
    public static string ShouldRoute(object? input) => ""yes"";
}

[FoundryAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphConditionMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF028_WhenConditionMethodWrongParameterType()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEntry(""Pipeline"")]
[{|FDRYMAF028:AgentGraphEdge(""Pipeline"", typeof(AgentB), Condition = ""ShouldRoute"")|}]
public class AgentA
{
    public static bool ShouldRoute(int x) => true;
}

[FoundryAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphConditionMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF028_WhenConditionMethodNotPublic()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
[AgentGraphEntry(""Pipeline"")]
[{|FDRYMAF028:AgentGraphEdge(""Pipeline"", typeof(AgentB), Condition = ""ShouldRoute"")|}]
public class AgentA
{
    private static bool ShouldRoute(object? x) => true;
}

[FoundryAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphConditionMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenConditionMethodDefinedOnBaseClass()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class BaseAgent
{
    public static bool ShouldRoute(object? input) => true;
}

[FoundryAgent]
[AgentGraphEntry(""Pipeline"")]
[AgentGraphEdge(""Pipeline"", typeof(AgentB), Condition = ""ShouldRoute"")]
public class AgentA : BaseAgent { }

[FoundryAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphConditionMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
