using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class AgentTopologyAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoError_WhenHandoffsTo_TargetHasFoundryAgent()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class GeographyAgent { }

[FoundryAgent]
[AgentHandoffsTo(typeof(GeographyAgent), ""geography questions"")]
public class TriageAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenClassHasNoHandoffsTo()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class GeographyAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF001_WhenHandoffsTo_TargetLacksFoundryAgent()
    {
        // FDRYMAF001 is reported on the attribute application span
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class GeographyAgent { }

[FoundryAgent]
[{|FDRYMAF001:AgentHandoffsTo(typeof(GeographyAgent), ""geography questions"")|}]
public class TriageAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_FDRYMAF003_WhenHandoffsTo_SourceLacksFoundryAgent()
    {
        // FDRYMAF003 is reported on the class name (typeSymbol.Locations[0])
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class GeographyAgent { }

[AgentHandoffsTo(typeof(GeographyAgent), ""geography questions"")]
public class {|FDRYMAF003:TriageAgent|} { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task BothDiagnostics_WhenBothSourceAndTargetLackFoundryAgent()
    {
        // FDRYMAF001 on attribute application, FDRYMAF003 on class name
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class GeographyAgent { }

[{|FDRYMAF001:AgentHandoffsTo(typeof(GeographyAgent), ""geography"")|}]
public class {|FDRYMAF003:TriageAgent|} { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_FDRYMAF001_MultipleHandoffTargets_OnlyReportsTheMissingOnes()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class GeographyAgent { }

public class LifestyleAgent { }

[FoundryAgent]
[AgentHandoffsTo(typeof(GeographyAgent), ""geography"")]
[{|FDRYMAF001:AgentHandoffsTo(typeof(LifestyleAgent), ""lifestyle"")|}]
public class TriageAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
