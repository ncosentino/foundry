using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class AgentFunctionGroupReferenceAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoWarning_WhenFunctionGroupsNotDeclared()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class GeographyAgent { }

[AgentFunctionGroup(""geography"")]
public class GeographyFunctions { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenFunctionGroupsIsEmpty()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent(FunctionGroups = new string[0])]
public class TriageAgent { }

[AgentFunctionGroup(""geography"")]
public class GeographyFunctions { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenAllGroupsAreRegistered()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent(FunctionGroups = new[] { ""geography"" })]
public class GeographyAgent { }

[AgentFunctionGroup(""geography"")]
public class GeographyFunctions { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenMultipleGroupsAllRegistered()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent(FunctionGroups = new[] { ""geography"", ""lifestyle"" })]
public class ExpertAgent { }

[AgentFunctionGroup(""geography"")]
public class GeographyFunctions { }

[AgentFunctionGroup(""lifestyle"")]
public class LifestyleFunctions { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_FDRYMAF005_WhenGroupNotRegistered()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[{|FDRYMAF005:FoundryAgent(FunctionGroups = new[] { ""unknown-group"" })|}]
public class GeographyAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_FDRYMAF005_WhenOneOfMultipleGroupsNotRegistered()
    {
        // "geography" is registered; "typo-group" is not
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[{|FDRYMAF005:FoundryAgent(FunctionGroups = new[] { ""geography"", ""typo-group"" })|}]
public class ExpertAgent { }

[AgentFunctionGroup(""geography"")]
public class GeographyFunctions { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_FDRYMAF005_OnEachAgentWithUnknownGroup()
    {
        // Two agents each reference an unregistered group — two warnings
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[{|FDRYMAF005:FoundryAgent(FunctionGroups = new[] { ""missing-a"" })|}]
public class AgentA { }

[{|FDRYMAF005:FoundryAgent(FunctionGroups = new[] { ""missing-b"" })|}]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenGroupRegisteredOnClassInSameCompilation()
    {
        // The [AgentFunctionGroup] class is in the same compilation — should be resolved
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent(FunctionGroups = new[] { ""tools"" })]
public class HelperAgent { }

[AgentFunctionGroup(""tools"")]
[AgentFunctionGroup(""extras"")]
public class MultiGroupFunctions { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
