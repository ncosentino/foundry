using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class AgentFunctionTypesMiswiredAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoWarning_WhenFunctionTypeHasAgentFunctionMethod()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class MyFunctions
{
    [AgentFunction]
    public string GetData() => """";
}

[FoundryAgent(FunctionTypes = new[] { typeof(MyFunctions) })]
public class MyAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionTypesMiswiredAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenNoFunctionTypesSpecified()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class MyAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionTypesMiswiredAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenFunctionTypesIsEmpty()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent(FunctionTypes = new System.Type[0])]
public class MyAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionTypesMiswiredAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_FDRYMAF014_WhenFunctionTypeHasNoAgentFunctionMethods()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class EmptyFunctions
{
    public string NotAnAgentFunction() => """";
}

[{|FDRYMAF014:FoundryAgent(FunctionTypes = new[] { typeof(EmptyFunctions) })|}]
public class MyAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionTypesMiswiredAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_FDRYMAF014_ForEachMiswiredType()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class EmptyFunctionsA
{
    public string NotAnAgentFunction() => """";
}

public class EmptyFunctionsB { }

[{|FDRYMAF014:FoundryAgent(FunctionTypes = new[] { typeof(EmptyFunctionsA) })|}]
public class AgentA { }

[{|FDRYMAF014:FoundryAgent(FunctionTypes = new[] { typeof(EmptyFunctionsB) })|}]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionTypesMiswiredAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_FDRYMAF014_OnlyForTypesWithoutAgentFunctions()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class GoodFunctions
{
    [AgentFunction]
    public string DoWork() => """";
}

public class EmptyFunctions { }

[{|FDRYMAF014:FoundryAgent(FunctionTypes = new[] { typeof(GoodFunctions), typeof(EmptyFunctions) })|}]
public class MyAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionTypesMiswiredAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
