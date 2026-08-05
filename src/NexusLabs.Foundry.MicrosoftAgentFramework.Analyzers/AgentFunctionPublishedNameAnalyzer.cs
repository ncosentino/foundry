using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers;

/// <summary>
/// Validates the function and parameter names an <c>[AgentFunction]</c> publishes through MEAI.
/// </summary>
/// <remarks>
/// <para>
/// <b>FDRYMAF032</b> (Error): An <c>[AIFunctionName]</c> or <c>[AIParameterName]</c>
/// value is null, empty, or whitespace.
/// </para>
/// <para>
/// <b>FDRYMAF033</b> (Error): Two functions in one type publish the same function name, or two
/// parameters in one function publish the same parameter name.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentFunctionPublishedNameAnalyzer : DiagnosticAnalyzer
{
    private const string AgentFunctionAttributeName =
        "NexusLabs.Foundry.MicrosoftAgentFramework.AgentFunctionAttribute";
    private const string AIFunctionNameAttributeName =
        "Microsoft.Extensions.AI.AIFunctionNameAttribute";
    private const string AIParameterNameAttributeName =
        "Microsoft.Extensions.AI.AIParameterNameAttribute";
    private const string CancellationTokenTypeName = "System.Threading.CancellationToken";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            MafDiagnosticDescriptors.InvalidAgentFunctionPublishedName,
            MafDiagnosticDescriptors.DuplicateAgentFunctionPublishedName);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var functions = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method =>
                method.MethodKind == MethodKind.Ordinary &&
                GetAttribute(method.GetAttributes(), AgentFunctionAttributeName) is not null)
            .Select(method => CreateFunctionDeclaration(method, context))
            .ToList();

        foreach (var collision in functions
            .Where(function => !string.IsNullOrWhiteSpace(function.PublishedName))
            .GroupBy(function => function.PublishedName!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            var participants = collision
                .OrderBy(function => function.Method.Name, StringComparer.Ordinal)
                .ToList();
            var description = string.Join(
                " and ",
                participants.Select(function => $"'{function.Method.Name}'"));

            foreach (var participant in participants)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MafDiagnosticDescriptors.DuplicateAgentFunctionPublishedName,
                    participant.Location,
                    "function",
                    collision.Key,
                    description,
                    type.ToDisplayString(),
                    "function names must be unique within a function type"));
            }
        }
    }

    private static FunctionDeclaration CreateFunctionDeclaration(
        IMethodSymbol method,
        SymbolAnalysisContext context)
    {
        var functionAttribute = GetAttribute(
            method.GetAttributes(),
            AgentFunctionAttributeName);
        var nameAttribute = GetAttribute(method.GetAttributes(), AIFunctionNameAttributeName);
        var publishedName = GetPublishedName(nameAttribute, method.Name);
        var location = GetLocation(nameAttribute ?? functionAttribute, method);

        if (nameAttribute is not null && string.IsNullOrWhiteSpace(publishedName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MafDiagnosticDescriptors.InvalidAgentFunctionPublishedName,
                location,
                "function",
                method.ToDisplayString()));
        }

        AnalyzeParameters(method, context);
        return new FunctionDeclaration(method, publishedName, location);
    }

    private static void AnalyzeParameters(
        IMethodSymbol method,
        SymbolAnalysisContext context)
    {
        var parameters = method.Parameters
            .Where(parameter => parameter.Type.ToDisplayString() != CancellationTokenTypeName)
            .Select(parameter =>
            {
                var nameAttribute = GetAttribute(
                    parameter.GetAttributes(),
                    AIParameterNameAttributeName);
                var publishedName = GetPublishedName(nameAttribute, parameter.Name);
                var location = GetLocation(nameAttribute, parameter);

                if (nameAttribute is not null && string.IsNullOrWhiteSpace(publishedName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MafDiagnosticDescriptors.InvalidAgentFunctionPublishedName,
                        location,
                        "parameter",
                        $"{method.ToDisplayString()}.{parameter.Name}"));
                }

                return new ParameterDeclaration(parameter, publishedName, location);
            })
            .ToList();

        foreach (var collision in parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.PublishedName))
            .GroupBy(parameter => parameter.PublishedName!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            var participants = collision
                .OrderBy(parameter => parameter.Parameter.Ordinal)
                .ToList();
            var description = string.Join(
                " and ",
                participants.Select(parameter => $"'{parameter.Parameter.Name}'"));

            foreach (var participant in participants)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MafDiagnosticDescriptors.DuplicateAgentFunctionPublishedName,
                    participant.Location,
                    "parameter",
                    collision.Key,
                    description,
                    method.ToDisplayString(),
                    "parameter names must be unique within a function"));
            }
        }
    }

    private static AttributeData? GetAttribute(
        ImmutableArray<AttributeData> attributes,
        string attributeName) =>
        attributes.FirstOrDefault(
            attribute => attribute.AttributeClass?.ToDisplayString() == attributeName);

    private static string? GetPublishedName(
        AttributeData? attribute,
        string fallback) =>
        attribute is null
            ? fallback
            : attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string
                : null;

    private static Location GetLocation(AttributeData? attribute, ISymbol symbol) =>
        attribute?.ApplicationSyntaxReference?.SyntaxTree is { } tree
            ? Location.Create(tree, attribute.ApplicationSyntaxReference.Span)
            : symbol.Locations[0];

    private readonly struct FunctionDeclaration
    {
        internal FunctionDeclaration(
            IMethodSymbol method,
            string? publishedName,
            Location location)
        {
            Method = method;
            PublishedName = publishedName;
            Location = location;
        }

        internal IMethodSymbol Method { get; }

        internal string? PublishedName { get; }

        internal Location Location { get; }
    }

    private readonly struct ParameterDeclaration
    {
        internal ParameterDeclaration(
            IParameterSymbol parameter,
            string? publishedName,
            Location location)
        {
            Parameter = parameter;
            PublishedName = publishedName;
            Location = location;
        }

        internal IParameterSymbol Parameter { get; }

        internal string? PublishedName { get; }

        internal Location Location { get; }
    }
}
