using System.Collections.Concurrent;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers;

/// <summary>
/// Analyzer that detects agent types that participate in no topology declaration.
/// </summary>
/// <remarks>
/// <b>FDRYMAF008</b> (Info): A class decorated with <c>[FoundryAgent]</c> is not referenced in any
/// topology attribute (<c>[AgentHandoffsTo]</c>, <c>[AgentGroupChatMember]</c>, or
/// <c>[AgentSequenceMember]</c>). This may indicate an orphaned or work-in-progress agent.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentOrphanAnalyzer : DiagnosticAnalyzer
{
    private const string FoundryAgentAttributeName = "NexusLabs.Foundry.MicrosoftAgentFramework.FoundryAgentAttribute";
    private const string AgentHandoffsToAttributeName = "NexusLabs.Foundry.MicrosoftAgentFramework.AgentHandoffsToAttribute";
    private const string AgentGroupChatMemberAttributeName = "NexusLabs.Foundry.MicrosoftAgentFramework.AgentGroupChatMemberAttribute";
    private const string AgentSequenceMemberAttributeName = "NexusLabs.Foundry.MicrosoftAgentFramework.AgentSequenceMemberAttribute";
    private const string AgentGraphEdgeAttributeName = "NexusLabs.Foundry.MicrosoftAgentFramework.AgentGraphEdgeAttribute";
    private const string AgentGraphEntryAttributeName = "NexusLabs.Foundry.MicrosoftAgentFramework.AgentGraphEntryAttribute";
    private const string AgentGraphNodeAttributeName = "NexusLabs.Foundry.MicrosoftAgentFramework.AgentGraphNodeAttribute";
    private const string AgentGraphReducerAttributeName = "NexusLabs.Foundry.MicrosoftAgentFramework.AgentGraphReducerAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.OrphanAgent);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var agents = new ConcurrentDictionary<string, (INamedTypeSymbol Symbol, Location Location)>(StringComparer.Ordinal);
            var topologyParticipants = new ConcurrentBag<string>();

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;
                var fqn = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                Location? agentAttrLocation = null;
                bool inTopology = false;

                foreach (var attr in typeSymbol.GetAttributes())
                {
                    var attrName = attr.AttributeClass?.ToDisplayString();

                    if (attrName == FoundryAgentAttributeName)
                    {
                        agentAttrLocation = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                            ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                            : typeSymbol.Locations[0];
                    }

                    if (attrName == AgentGroupChatMemberAttributeName ||
                        attrName == AgentSequenceMemberAttributeName ||
                        attrName == AgentGraphEntryAttributeName ||
                        attrName == AgentGraphNodeAttributeName ||
                        attrName == AgentGraphReducerAttributeName)
                        inTopology = true;

                    if (attrName == AgentGraphEdgeAttributeName)
                    {
                        inTopology = true;

                        // The edge target is also a topology participant
                        if (attr.ConstructorArguments.Length >= 2
                            && attr.ConstructorArguments[1].Kind == TypedConstantKind.Type
                            && attr.ConstructorArguments[1].Value is INamedTypeSymbol edgeTargetType)
                        {
                            topologyParticipants.Add(
                                edgeTargetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                        }
                    }

                    if (attrName == AgentHandoffsToAttributeName)
                    {
                        inTopology = true;

                        // The handoff target is also a topology participant
                        if (attr.ConstructorArguments.Length >= 1
                            && attr.ConstructorArguments[0].Kind == TypedConstantKind.Type
                            && attr.ConstructorArguments[0].Value is INamedTypeSymbol targetType)
                        {
                            topologyParticipants.Add(
                                targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                        }
                    }
                }

                if (agentAttrLocation is not null)
                    agents[fqn] = (typeSymbol, agentAttrLocation);

                if (inTopology)
                    topologyParticipants.Add(fqn);
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                var participantSet = new HashSet<string>(topologyParticipants, StringComparer.Ordinal);

                foreach (var kvp in agents)
                {
                    if (!participantSet.Contains(kvp.Key))
                    {
                        endContext.ReportDiagnostic(Diagnostic.Create(
                            MafDiagnosticDescriptors.OrphanAgent,
                            kvp.Value.Location,
                            kvp.Value.Symbol.Name));
                    }
                }
            });
        });
    }
}
