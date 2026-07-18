using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers;

/// <summary>
/// Analyzer that validates <c>[AgentHandoffsTo]</c> topology declarations.
/// </summary>
/// <remarks>
/// <para>
/// <b>FDRYMAF001</b> (Error): The target type referenced by <c>[AgentHandoffsTo(typeof(X))]</c> is not
/// decorated with <c>[FoundryAgent]</c>. Handoff targets must be registered agent types.
/// </para>
/// <para>
/// <b>FDRYMAF003</b> (Warning): The class carrying <c>[AgentHandoffsTo]</c> is not itself decorated with
/// <c>[FoundryAgent]</c>. The initial agent in a handoff workflow must be a declared agent.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentTopologyAnalyzer : DiagnosticAnalyzer
{
    private const string AgentHandoffsToAttributeName = "NexusLabs.Foundry.MicrosoftAgentFramework.AgentHandoffsToAttribute";
    private const string FoundryAgentAttributeName = "NexusLabs.Foundry.MicrosoftAgentFramework.FoundryAgentAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            MafDiagnosticDescriptors.HandoffsToTargetNotFoundryAgent,
            MafDiagnosticDescriptors.HandoffsToSourceNotFoundryAgent);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        var handoffsToAttrs = typeSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == AgentHandoffsToAttributeName)
            .ToImmutableArray();

        if (handoffsToAttrs.IsEmpty)
            return;

        // FDRYMAF003: source class lacks [FoundryAgent]
        var hasFoundryAgent = typeSymbol.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == FoundryAgentAttributeName);

        if (!hasFoundryAgent)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MafDiagnosticDescriptors.HandoffsToSourceNotFoundryAgent,
                typeSymbol.Locations[0],
                typeSymbol.Name));
        }

        // FDRYMAF001: each target type must have [FoundryAgent]
        foreach (var attr in handoffsToAttrs)
        {
            if (attr.ConstructorArguments.Length < 1)
                continue;

            var typeArg = attr.ConstructorArguments[0];
            if (typeArg.Kind != TypedConstantKind.Type || typeArg.Value is not INamedTypeSymbol targetType)
                continue;

            var targetHasFoundryAgent = targetType.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == FoundryAgentAttributeName);

            if (!targetHasFoundryAgent)
            {
                // Report on the attribute usage location when available, otherwise fall back to the class
                var location = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                    ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                    : typeSymbol.Locations[0];

                context.ReportDiagnostic(Diagnostic.Create(
                    MafDiagnosticDescriptors.HandoffsToTargetNotFoundryAgent,
                    location,
                    targetType.Name));
            }
        }
    }
}
