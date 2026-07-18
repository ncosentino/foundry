using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class AgentTopologyCodeFixTests
{
    [Fact]
    public async Task Fix_FDRYMAF001_AddsFoundryAgentToTargetType()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

public class GeographyAgent { }

[FoundryAgent]
[{|FDRYMAF001:AgentHandoffsTo(typeof(GeographyAgent), ""geography questions"")|}]
public class TriageAgent { }
" + MafTestAttributes.All;

        var fixedCode = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class GeographyAgent { }

[FoundryAgent]
[AgentHandoffsTo(typeof(GeographyAgent), ""geography questions"")]
public class TriageAgent { }
" + MafTestAttributes.All;

        var test = new CSharpCodeFixTest<AgentTopologyAnalyzer, AgentTopologyCodeFixProvider, DefaultVerifier>
        {
            TestCode = code,
            FixedCode = fixedCode
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Fix_FDRYMAF003_AddsFoundryAgentToSourceClass()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class GeographyAgent { }

[AgentHandoffsTo(typeof(GeographyAgent), ""geography questions"")]
public class {|FDRYMAF003:TriageAgent|} { }
" + MafTestAttributes.All;

        var fixedCode = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class GeographyAgent { }

[FoundryAgent]
[AgentHandoffsTo(typeof(GeographyAgent), ""geography questions"")]
public class TriageAgent { }
" + MafTestAttributes.All;

        var test = new CSharpCodeFixTest<AgentTopologyAnalyzer, AgentTopologyCodeFixProvider, DefaultVerifier>
        {
            TestCode = code,
            FixedCode = fixedCode
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
