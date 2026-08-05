using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class AgentFunctionPublishedNameAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoError_WhenPublishedNamesAreDistinct()
    {
        var code = @"
using Microsoft.Extensions.AI;
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class Tools
{
    [AgentFunction]
    [AIFunctionName(""first_tool"")]
    public string First([AIParameterName(""first_value"")] string value) => value;

    [AgentFunction]
    [AIFunctionName(""second_tool"")]
    public string Second([AIParameterName(""second_value"")] string value) => value;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionPublishedNameAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Error_WhenPublishedFunctionNameIsBlank(string name)
    {
        var code = $@"
using Microsoft.Extensions.AI;
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class Tools
{{
    [AgentFunction]
    [{{|#0:AIFunctionName(""{name}"")|}}]
    public string Run(string value) => value;
}}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionPublishedNameAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(
                    MafDiagnosticDescriptors.InvalidAgentFunctionPublishedName)
                    .WithLocation(0)
                    .WithArguments("function", "Tools.Run(string)"),
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenPublishedParameterNameIsBlank()
    {
        var code = @"
using Microsoft.Extensions.AI;
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class Tools
{
    [AgentFunction]
    public string Run(
        [{|#0:AIParameterName("""")|}] string value) => value;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionPublishedNameAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(
                    MafDiagnosticDescriptors.InvalidAgentFunctionPublishedName)
                    .WithLocation(0)
                    .WithArguments("parameter", "Tools.Run(string).value"),
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenFunctionsInOneTypePublishTheSameName()
    {
        var code = @"
using Microsoft.Extensions.AI;
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class Tools
{
    [AgentFunction]
    [{|#0:AIFunctionName(""shared_tool"")|}]
    public string First(string value) => value;

    [AgentFunction]
    [{|#1:AIFunctionName(""shared_tool"")|}]
    public string Second(string value) => value;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionPublishedNameAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(
                    MafDiagnosticDescriptors.DuplicateAgentFunctionPublishedName)
                    .WithLocation(0)
                    .WithArguments(
                        "function",
                        "shared_tool",
                        "'First' and 'Second'",
                        "Tools",
                        "function names must be unique within a function type"),
                new DiagnosticResult(
                    MafDiagnosticDescriptors.DuplicateAgentFunctionPublishedName)
                    .WithLocation(1)
                    .WithArguments(
                        "function",
                        "shared_tool",
                        "'First' and 'Second'",
                        "Tools",
                        "function names must be unique within a function type"),
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenPublishedNameCollidesWithAnotherMethodName()
    {
        var code = @"
using Microsoft.Extensions.AI;
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class Tools
{
    [{|#0:AgentFunction|}]
    public string Search(string value) => value;

    [AgentFunction]
    [{|#1:AIFunctionName(""Search"")|}]
    public string Lookup(string value) => value;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionPublishedNameAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(
                    MafDiagnosticDescriptors.DuplicateAgentFunctionPublishedName)
                    .WithLocation(0)
                    .WithArguments(
                        "function",
                        "Search",
                        "'Lookup' and 'Search'",
                        "Tools",
                        "function names must be unique within a function type"),
                new DiagnosticResult(
                    MafDiagnosticDescriptors.DuplicateAgentFunctionPublishedName)
                    .WithLocation(1)
                    .WithArguments(
                        "function",
                        "Search",
                        "'Lookup' and 'Search'",
                        "Tools",
                        "function names must be unique within a function type"),
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenParametersPublishTheSameName()
    {
        var code = @"
using Microsoft.Extensions.AI;
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class Tools
{
    [AgentFunction]
    public string Run(
        [{|#0:AIParameterName(""same"")|}] string first,
        [{|#1:AIParameterName(""same"")|}] string second) => first + second;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionPublishedNameAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(
                    MafDiagnosticDescriptors.DuplicateAgentFunctionPublishedName)
                    .WithLocation(0)
                    .WithArguments(
                        "parameter",
                        "same",
                        "'first' and 'second'",
                        "Tools.Run(string, string)",
                        "parameter names must be unique within a function"),
                new DiagnosticResult(
                    MafDiagnosticDescriptors.DuplicateAgentFunctionPublishedName)
                    .WithLocation(1)
                    .WithArguments(
                        "parameter",
                        "same",
                        "'first' and 'second'",
                        "Tools.Run(string, string)",
                        "parameter names must be unique within a function"),
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenSameNameExistsInSeparateFunctionTypes()
    {
        var code = @"
using Microsoft.Extensions.AI;
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class SupportTools
{
    [AgentFunction]
    [AIFunctionName(""search"")]
    public string Search(string value) => value;
}

public class BillingTools
{
    [AgentFunction]
    [AIFunctionName(""search"")]
    public string Search(string value) => value;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionPublishedNameAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
