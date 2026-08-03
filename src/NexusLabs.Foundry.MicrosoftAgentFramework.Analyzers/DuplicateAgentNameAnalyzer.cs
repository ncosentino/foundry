using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers;

/// <summary>
/// Analyzer that detects two declared agents publishing the same name.
/// </summary>
/// <remarks>
/// <para>
/// <b>FDRYMAF031</b> (Error): Two or more classes decorated with <c>[FoundryAgent]</c> resolve to
/// the same published name — <c>[FoundryAgent(Name = "…")]</c> when declared, the class name
/// otherwise.
/// </para>
/// <para>
/// Building the agent factory already rejects this, but only once the composition root runs. The
/// declarations that collide are visible at compile time, so the collision is reported there
/// instead, next to the code that has to change.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateAgentNameAnalyzer : DiagnosticAnalyzer
{
    private const string FoundryAgentAttributeName = "NexusLabs.Foundry.MicrosoftAgentFramework.FoundryAgentAttribute";
    private const string NameProperty = "Name";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.DuplicateAgentName);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var declarations = new ConcurrentBag<(
                string PublishedName,
                bool IsDeclared,
                string FullName,
                Location Location)>();

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;

                foreach (var attribute in typeSymbol.GetAttributes())
                {
                    if (attribute.AttributeClass?.ToDisplayString() != FoundryAgentAttributeName)
                    {
                        continue;
                    }

                    var location = attribute.ApplicationSyntaxReference?.SyntaxTree is { } tree
                        ? Location.Create(tree, attribute.ApplicationSyntaxReference.Span)
                        : typeSymbol.Locations[0];

                    var declaredName = GetDeclaredName(attribute);
                    declarations.Add((
                        PublishedName: string.IsNullOrWhiteSpace(declaredName)
                            ? typeSymbol.Name
                            : declaredName!,
                        IsDeclared: !string.IsNullOrWhiteSpace(declaredName),
                        FullName: typeSymbol.ToDisplayString(),
                        Location: location));
                    break;
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                var collisions = declarations
                    .GroupBy(declaration => declaration.PublishedName, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1);

                foreach (var collision in collisions)
                {
                    // Ordering keeps the description stable regardless of the order symbols were
                    // visited in, which is not deterministic under concurrent execution.
                    var participants = collision
                        .OrderBy(declaration => declaration.FullName, StringComparer.Ordinal)
                        .ToList();

                    var description = string.Join(
                        " and ",
                        participants.Select(declaration => declaration.IsDeclared
                            ? $"'{declaration.FullName}' (declared)"
                            : $"'{declaration.FullName}' (from its class name)"));

                    foreach (var participant in participants)
                    {
                        endContext.ReportDiagnostic(Diagnostic.Create(
                            MafDiagnosticDescriptors.DuplicateAgentName,
                            participant.Location,
                            collision.Key,
                            description));
                    }
                }
            });
        });
    }

    private static string? GetDeclaredName(AttributeData attribute)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == NameProperty && argument.Value.Value is string name)
            {
                return name;
            }
        }

        return null;
    }
}
