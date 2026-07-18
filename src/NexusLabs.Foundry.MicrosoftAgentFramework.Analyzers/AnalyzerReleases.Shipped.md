; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.0.2

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
FDRYMAF001 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | AgentHandoffsTo target type is not decorated with [FoundryAgent]
FDRYMAF002 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | AgentGroupChatMember group has fewer than two members in this compilation
FDRYMAF003 | NexusLabs.Foundry.MicrosoftAgentFramework | Warning | Class with [AgentHandoffsTo] is not itself decorated with [FoundryAgent]
FDRYMAF004 | NexusLabs.Foundry.MicrosoftAgentFramework | Warning | Cyclic handoff chain detected
FDRYMAF005 | NexusLabs.Foundry.MicrosoftAgentFramework | Warning | FunctionGroups references a group name with no matching [AgentFunctionGroup] class
FDRYMAF006 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | Duplicate Order value in [AgentSequenceMember] pipeline
FDRYMAF007 | NexusLabs.Foundry.MicrosoftAgentFramework | Warning | Gap in [AgentSequenceMember] Order sequence
FDRYMAF008 | NexusLabs.Foundry.MicrosoftAgentFramework | Info | Agent participates in no topology declaration
FDRYMAF009 | NexusLabs.Foundry.MicrosoftAgentFramework | Warning | [WorkflowRunTerminationCondition] declared on a non-agent class
FDRYMAF010 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | Termination condition type does not implement IWorkflowTerminationCondition
FDRYMAF011 | NexusLabs.Foundry.MicrosoftAgentFramework | Info | Prefer [AgentTerminationCondition] over [WorkflowRunTerminationCondition] for group chat members
FDRYMAF012 | NexusLabs.Foundry.MicrosoftAgentFramework | Warning | [AgentFunction] method is missing a [Description] attribute
FDRYMAF013 | NexusLabs.Foundry.MicrosoftAgentFramework | Warning | [AgentFunction] method parameter is missing a [Description] attribute
FDRYMAF014 | NexusLabs.Foundry.MicrosoftAgentFramework | Warning | FunctionTypes entry has no [AgentFunction] methods
FDRYMAF015 | NexusLabs.Foundry.MicrosoftAgentFramework | Warning | ToolResultToStringAnalyzer, Do not call ToString() on tool result objects
FDRYMAF016 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | AgentGraphCycleAnalyzer, Cycle detected in agent graph
FDRYMAF017 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | AgentGraphEntryPointAnalyzer, Graph has no entry point
FDRYMAF018 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | AgentGraphEntryPointAnalyzer, Graph has multiple entry points
FDRYMAF019 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | AgentGraphTopologyAnalyzer, Graph edge target is not a declared agent
FDRYMAF020 | NexusLabs.Foundry.MicrosoftAgentFramework | Warning | AgentGraphTopologyAnalyzer, Graph edge source is not a declared agent
FDRYMAF021 | NexusLabs.Foundry.MicrosoftAgentFramework | Warning | AgentGraphTopologyAnalyzer, Graph entry point is not a declared agent
FDRYMAF022 | NexusLabs.Foundry.MicrosoftAgentFramework | Warning | AgentGraphReachabilityAnalyzer, Graph contains unreachable agents
FDRYMAF024 | NexusLabs.Foundry.MicrosoftAgentFramework | Warning | AgentGraphOptionalFanOutAnalyzer, All edges from fan-out node are optional
FDRYMAF025 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | WaitAnyCreateGraphAnalyzer, CreateGraphWorkflow is incompatible with GraphJoinMode.WaitAny
FDRYMAF027 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | AgentGraphTerminalNodeAnalyzer, Terminal node has outgoing edges
FDRYMAF028 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | AgentGraphConditionMethodAnalyzer, Condition method not found or has wrong signature
FDRYMAF029 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | AgentGraphReducerMethodAnalyzer, Reducer method not found or has wrong signature
FDRYMAF030 | NexusLabs.Foundry.MicrosoftAgentFramework | Info | AgentFunctionJsonStringParameterAnalyzer, AgentFunction string parameter described as JSON could be JsonElement
