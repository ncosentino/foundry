using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.Tests;

public sealed class DuplicateAgentNameAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoError_WhenPublishedNamesAreDistinct()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[FoundryAgent]
public class TriageAgent { }

[FoundryAgent]
public class ExpertAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<DuplicateAgentNameAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenTwoClassNamesCollideAcrossNamespaces()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace Support
{
    [{|#0:FoundryAgent|}]
    public class TriageAgent { }
}

namespace Billing
{
    [{|#1:FoundryAgent|}]
    public class TriageAgent { }
}
" + Attributes;

        var expectedDescription =
            "'Billing.TriageAgent' (from its class name) and 'Support.TriageAgent' (from its class name)";

        var test = new CSharpAnalyzerTest<DuplicateAgentNameAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(MafDiagnosticDescriptors.DuplicateAgentName)
                    .WithLocation(0)
                    .WithArguments("TriageAgent", expectedDescription),
                new DiagnosticResult(MafDiagnosticDescriptors.DuplicateAgentName)
                    .WithLocation(1)
                    .WithArguments("TriageAgent", expectedDescription),
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <remarks>
    /// Declaring a distinct name is the documented fix for a class-name collision, so it has to
    /// actually clear the diagnostic.
    /// </remarks>
    [Fact]
    public async Task NoError_WhenADeclaredNameResolvesAClassNameCollision()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace Support
{
    [FoundryAgent(Name = ""SupportTriage"")]
    public class TriageAgent { }
}

namespace Billing
{
    [FoundryAgent]
    public class TriageAgent { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DuplicateAgentNameAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenTwoDeclaredNamesCollide()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[{|#0:FoundryAgent(Name = ""Triage"")|}]
public class SupportTriageAgent { }

[{|#1:FoundryAgent(Name = ""Triage"")|}]
public class BillingTriageAgent { }
" + Attributes;

        var expectedDescription =
            "'BillingTriageAgent' (declared) and 'SupportTriageAgent' (declared)";

        var test = new CSharpAnalyzerTest<DuplicateAgentNameAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(MafDiagnosticDescriptors.DuplicateAgentName)
                    .WithLocation(0)
                    .WithArguments("Triage", expectedDescription),
                new DiagnosticResult(MafDiagnosticDescriptors.DuplicateAgentName)
                    .WithLocation(1)
                    .WithArguments("Triage", expectedDescription),
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <remarks>
    /// A declared name colliding with another agent's class name is the collision most likely to be
    /// introduced accidentally, because only one of the two files mentions the shared name.
    /// </remarks>
    [Fact]
    public async Task Error_WhenADeclaredNameCollidesWithAnotherAgentClassName()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

[{|#0:FoundryAgent(Name = ""ExpertAgent"")|}]
public class TriageAgent { }

[{|#1:FoundryAgent|}]
public class ExpertAgent { }
" + Attributes;

        var expectedDescription =
            "'ExpertAgent' (from its class name) and 'TriageAgent' (declared)";

        var test = new CSharpAnalyzerTest<DuplicateAgentNameAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(MafDiagnosticDescriptors.DuplicateAgentName)
                    .WithLocation(1)
                    .WithArguments("ExpertAgent", expectedDescription),
                new DiagnosticResult(MafDiagnosticDescriptors.DuplicateAgentName)
                    .WithLocation(0)
                    .WithArguments("ExpertAgent", expectedDescription),
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <remarks>
    /// A blank declared name falls back to the class name at runtime, so the analyzer has to fall
    /// back identically or it would disagree with what the factory actually registers.
    /// </remarks>
    [Fact]
    public async Task Error_WhenABlankDeclaredNameFallsBackToACollidingClassName()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace Support
{
    [{|#0:FoundryAgent(Name = ""   "")|}]
    public class TriageAgent { }
}

namespace Billing
{
    [{|#1:FoundryAgent|}]
    public class TriageAgent { }
}
" + Attributes;

        var expectedDescription =
            "'Billing.TriageAgent' (from its class name) and 'Support.TriageAgent' (from its class name)";

        var test = new CSharpAnalyzerTest<DuplicateAgentNameAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(MafDiagnosticDescriptors.DuplicateAgentName)
                    .WithLocation(0)
                    .WithArguments("TriageAgent", expectedDescription),
                new DiagnosticResult(MafDiagnosticDescriptors.DuplicateAgentName)
                    .WithLocation(1)
                    .WithArguments("TriageAgent", expectedDescription),
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenAClassWithoutTheAttributeSharesAName()
    {
        var code = @"
using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace Support
{
    [FoundryAgent]
    public class TriageAgent { }
}

namespace Billing
{
    public class TriageAgent { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DuplicateAgentNameAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
