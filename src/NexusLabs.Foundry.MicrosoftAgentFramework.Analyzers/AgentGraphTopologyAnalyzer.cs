using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers;

/// <summary>
/// Analyzer that validates graph edge and entry point declarations reference declared agents.
/// </summary>
/// <remarks>
/// <para>
/// <b>FDRYMAF019</b> (Error): An <c>[AgentGraphEdge]</c> target type is not decorated with
/// <c>[FoundryAgent]</c>.
/// </para>
/// <para>
/// <b>FDRYMAF020</b> (Warning): A class has <c>[AgentGraphEdge]</c> but is not itself decorated
/// with <c>[FoundryAgent]</c>.
/// </para>
/// <para>
/// <b>FDRYMAF021</b> (Warning): A class has <c>[AgentGraphEntry]</c> but is not itself decorated
/// with <c>[FoundryAgent]</c>.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentGraphTopologyAnalyzer : DiagnosticAnalyzer
{
    private const string AgentGraphEdgeAttributeName = "NexusLabs.Foundry.MicrosoftAgentFramework.AgentGraphEdgeAttribute";
    private const string AgentGraphEntryAttributeName = "NexusLabs.Foundry.MicrosoftAgentFramework.AgentGraphEntryAttribute";
    private const string FoundryAgentAttributeName = "NexusLabs.Foundry.MicrosoftAgentFramework.FoundryAgentAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            MafDiagnosticDescriptors.GraphEdgeTargetNotAgent,
            MafDiagnosticDescriptors.GraphEdgeSourceNotAgent,
            MafDiagnosticDescriptors.GraphEntryPointNotAgent);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;
        var attributes = typeSymbol.GetAttributes();

        var hasFoundryAgent = attributes
            .Any(a => a.AttributeClass?.ToDisplayString() == FoundryAgentAttributeName);

        var graphEdgeAttrs = attributes
            .Where(a => a.AttributeClass?.ToDisplayString() == AgentGraphEdgeAttributeName)
            .ToImmutableArray();

        var graphEntryAttrs = attributes
            .Where(a => a.AttributeClass?.ToDisplayString() == AgentGraphEntryAttributeName)
            .ToImmutableArray();

        // FDRYMAF020: source class has [AgentGraphEdge] but no [FoundryAgent]
        if (!graphEdgeAttrs.IsEmpty && !hasFoundryAgent)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MafDiagnosticDescriptors.GraphEdgeSourceNotAgent,
                typeSymbol.Locations[0],
                typeSymbol.Name));
        }

        // FDRYMAF021: source class has [AgentGraphEntry] but no [FoundryAgent]
        if (!graphEntryAttrs.IsEmpty && !hasFoundryAgent)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MafDiagnosticDescriptors.GraphEntryPointNotAgent,
                typeSymbol.Locations[0],
                typeSymbol.Name));
        }

        // FDRYMAF019: each edge target must have [FoundryAgent]
        foreach (var attr in graphEdgeAttrs)
        {
            if (attr.ConstructorArguments.Length < 2)
                continue;

            var targetTypeArg = attr.ConstructorArguments[1];
            if (targetTypeArg.Kind != TypedConstantKind.Type || targetTypeArg.Value is not INamedTypeSymbol targetType)
                continue;

            var targetHasFoundryAgent = targetType.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == FoundryAgentAttributeName);

            if (!targetHasFoundryAgent)
            {
                var location = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                    ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                    : typeSymbol.Locations[0];

                context.ReportDiagnostic(Diagnostic.Create(
                    MafDiagnosticDescriptors.GraphEdgeTargetNotAgent,
                    location,
                    targetType.Name));
            }
        }
    }
}
