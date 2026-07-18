namespace NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers;

/// <summary>
/// Contains diagnostic IDs for all Foundry Agent Framework analyzers.
/// </summary>
/// <remarks>
/// Agent Framework analyzer codes use the FDRYMAF prefix.
/// </remarks>
public static class MafDiagnosticIds
{
    /// <summary>
    /// FDRYMAF001: <c>[AgentHandoffsTo(typeof(X))]</c> target type X is not decorated with <c>[FoundryAgent]</c>.
    /// </summary>
    public const string HandoffsToTargetNotFoundryAgent = "FDRYMAF001";

    /// <summary>
    /// FDRYMAF002: <c>[AgentGroupChatMember("g")]</c> group "g" has fewer than two members in this compilation.
    /// </summary>
    public const string GroupChatTooFewMembers = "FDRYMAF002";

    /// <summary>
    /// FDRYMAF003: A class has <c>[AgentHandoffsTo]</c> but is not itself decorated with <c>[FoundryAgent]</c>.
    /// </summary>
    public const string HandoffsToSourceNotFoundryAgent = "FDRYMAF003";

    /// <summary>
    /// FDRYMAF004: A cyclic handoff chain was detected (e.g. A → B → A).
    /// </summary>
    public const string CyclicHandoffChain = "FDRYMAF004";

    /// <summary>
    /// FDRYMAF005: An agent declares a <c>FunctionGroups</c> entry whose name has no matching
    /// <c>[AgentFunctionGroup("name")]</c> class in this compilation.
    /// </summary>
    public const string UnresolvedFunctionGroupReference = "FDRYMAF005";

    /// <summary>
    /// FDRYMAF006: Two or more agents in the same <c>[AgentSequenceMember]</c> pipeline declare
    /// the same <c>Order</c> value.
    /// </summary>
    public const string DuplicateSequenceOrder = "FDRYMAF006";

    /// <summary>
    /// FDRYMAF007: The <c>Order</c> values within a named <c>[AgentSequenceMember]</c> pipeline
    /// are not contiguous (a gap exists).
    /// </summary>
    public const string GapInSequenceOrder = "FDRYMAF007";

    /// <summary>
    /// FDRYMAF008: A class decorated with <c>[FoundryAgent]</c> participates in no topology
    /// declaration (<c>[AgentHandoffsTo]</c>, <c>[AgentGroupChatMember]</c>, or
    /// <c>[AgentSequenceMember]</c>).
    /// </summary>
    public const string OrphanAgent = "FDRYMAF008";

    /// <summary>
    /// FDRYMAF009: <c>[WorkflowRunTerminationCondition]</c> is declared on a class that is not
    /// decorated with <c>[FoundryAgent]</c>.
    /// </summary>
    public const string WorkflowRunTerminationConditionOnNonAgent = "FDRYMAF009";

    /// <summary>
    /// FDRYMAF010: The <c>conditionType</c> passed to <c>[WorkflowRunTerminationCondition]</c>
    /// or <c>[AgentTerminationCondition]</c> does not implement
    /// <c>IWorkflowTerminationCondition</c>.
    /// </summary>
    public const string TerminationConditionTypeInvalid = "FDRYMAF010";

    /// <summary>
    /// FDRYMAF011: <c>[WorkflowRunTerminationCondition]</c> is declared on a
    /// <c>[AgentGroupChatMember]</c> class; prefer <c>[AgentTerminationCondition]</c> for group
    /// chat members.
    /// </summary>
    public const string PreferAgentTerminationConditionForGroupChat = "FDRYMAF011";

    /// <summary>
    /// FDRYMAF012: A method decorated with <c>[AgentFunction]</c> has no
    /// <c>[System.ComponentModel.Description]</c> attribute.
    /// </summary>
    public const string AgentFunctionMissingDescription = "FDRYMAF012";

    /// <summary>
    /// FDRYMAF013: A parameter of an <c>[AgentFunction]</c> method (other than
    /// <c>CancellationToken</c>) has no <c>[System.ComponentModel.Description]</c> attribute.
    /// </summary>
    public const string AgentFunctionParameterMissingDescription = "FDRYMAF013";

    /// <summary>
    /// FDRYMAF014: A type listed in <c>FunctionTypes</c> on a <c>[FoundryAgent]</c> has no
    /// methods decorated with <c>[AgentFunction]</c>, so the agent silently receives zero tools
    /// from that type.
    /// </summary>
    public const string AgentFunctionTypesMiswired = "FDRYMAF014";

    /// <summary>
    /// FDRYMAF015: <c>.ToString()</c> is called on <c>ToolCallResult.Result</c> or
    /// <c>FunctionResultContent.Result</c>, which are <c>object?</c> and may contain
    /// a <c>JsonElement</c>. Use <c>ToolResultSerializer.Serialize()</c> instead.
    /// </summary>
    public const string ToolResultToStringCall = "FDRYMAF015";

    /// <summary>
    /// FDRYMAF016: A cycle was detected in a named agent graph declared via
    /// <c>[AgentGraphEdge]</c> attributes.
    /// </summary>
    public const string GraphCycleDetected = "FDRYMAF016";

    /// <summary>
    /// FDRYMAF017: A named agent graph has no <c>[AgentGraphEntry]</c> declaration.
    /// </summary>
    public const string GraphNoEntryPoint = "FDRYMAF017";

    /// <summary>
    /// FDRYMAF018: A named agent graph has multiple <c>[AgentGraphEntry]</c> declarations.
    /// </summary>
    public const string GraphMultipleEntryPoints = "FDRYMAF018";

    /// <summary>
    /// FDRYMAF019: An <c>[AgentGraphEdge]</c> references a target type that is not
    /// decorated with <c>[FoundryAgent]</c>.
    /// </summary>
    public const string GraphEdgeTargetNotAgent = "FDRYMAF019";

    /// <summary>
    /// FDRYMAF020: A class has <c>[AgentGraphEdge]</c> but is not itself decorated
    /// with <c>[FoundryAgent]</c>.
    /// </summary>
    public const string GraphEdgeSourceNotAgent = "FDRYMAF020";

    /// <summary>
    /// FDRYMAF021: A class has <c>[AgentGraphEntry]</c> but is not itself decorated
    /// with <c>[FoundryAgent]</c>.
    /// </summary>
    public const string GraphEntryPointNotAgent = "FDRYMAF021";

    /// <summary>
    /// FDRYMAF022: A named agent graph contains agents that are not reachable from
    /// the entry point.
    /// </summary>
    public const string GraphUnreachableAgent = "FDRYMAF022";

    // FDRYMAF023 was previously used for MaxSupersteps validation.
    // The MaxSupersteps property was removed from AgentGraphEntryAttribute.
    // ID is retired — do not reuse.

    /// <summary>
    /// FDRYMAF024: All outgoing edges from a fan-out node have
    /// <c>IsRequired = false</c>, meaning the graph could produce empty results if all
    /// optional branches fail.
    /// </summary>
    public const string GraphAllEdgesOptional = "FDRYMAF024";

    /// <summary>
    /// FDRYMAF027: A terminal node (a node that should have no outgoing edges) has
    /// outgoing <c>[AgentGraphEdge]</c> declarations.
    /// </summary>
    public const string GraphTerminalNodeHasOutgoingEdges = "FDRYMAF027";

    /// <summary>
    /// FDRYMAF025: <c>CreateGraphWorkflow</c> is called on a graph that contains a
    /// <c>[AgentGraphNode(JoinMode = GraphJoinMode.WaitAny)]</c> declaration.
    /// <c>CreateGraphWorkflow</c> returns a MAF <c>Workflow</c> that uses BSP execution,
    /// which does not support WaitAny. Use <c>RunGraphAsync</c> instead.
    /// </summary>
    public const string WaitAnyIncompatibleWithCreateGraphWorkflow = "FDRYMAF025";

    // FDRYMAF026 — reserved (unused, do not reuse without deliberate assignment)

    /// <summary>
    /// FDRYMAF028: The <c>Condition</c> property on <c>[AgentGraphEdge]</c> references
    /// a method that does not exist on the source agent, is not static, or has the
    /// wrong signature. The method must be <c>static bool MethodName(object?)</c>.
    /// </summary>
    public const string GraphConditionMethodInvalid = "FDRYMAF028";

    /// <summary>
    /// FDRYMAF029: The <c>ReducerMethod</c> property on <c>[AgentGraphReducer]</c>
    /// references a method that does not exist on the decorated type, is not static,
    /// or has the wrong signature. The method must be
    /// <c>static string MethodName(IReadOnlyList&lt;string&gt;)</c>.
    /// </summary>
    public const string GraphReducerMethodInvalid = "FDRYMAF029";

    /// <summary>
    /// FDRYMAF030: An <c>[AgentFunction]</c> parameter typed as <c>string</c> whose name ends
    /// with <c>Json</c>/<c>_json</c> or whose <c>[Description]</c> mentions <c>"JSON array"</c>
    /// or <c>"JSON object"</c> could be typed as <c>System.Text.Json.JsonElement</c> for
    /// direct, typed access. Informational only — the kind-tolerant generator coercion makes
    /// the string-typed shape work via <c>JsonElement.GetRawText()</c>.
    /// </summary>
    public const string AgentFunctionJsonStringParameter = "FDRYMAF030";
}
