namespace NexusLabs.Foundry.MicrosoftAgentFramework;

/// <summary>
/// Declares a termination condition that is wired into the group chat manager for a group this
/// agent belongs to (Layer 1). The condition is evaluated inside MAF's group chat loop, before the
/// next agent turn starts, giving a clean early exit.
/// </summary>
/// <remarks>
/// <para>
/// Apply to a class also decorated with <see cref="AgentGroupChatMemberAttribute"/>. Multiple
/// conditions may be stacked; OR semantics apply (first match stops the workflow).
/// </para>
/// <para>
/// <b>The condition is evaluated after every participant's turn, not only after turns produced by
/// the agent it is declared on.</b> Every condition declared by any member of the group is
/// collected into one set and each is offered every turn, so the class this attribute sits on
/// determines only which group the condition joins — it does not scope when the condition runs.
/// A condition that should apply to one agent has to say so itself, by comparing
/// <see cref="TerminationContext.AgentId"/>:
/// </para>
/// <example>
/// <code>
/// public sealed class ApprovedByReviewer : IWorkflowTerminationCondition
/// {
///     public bool ShouldTerminate(TerminationContext context) =&gt;
///         context.AgentId == "ReviewerAgent"
///         &amp;&amp; context.LastMessage?.Text?.Contains("APPROVED") == true;
/// }
/// </code>
/// </example>
/// <para>
/// The <c>conditionType</c> must implement <see cref="IWorkflowTerminationCondition"/>.
/// Constructor arguments for the condition are passed via <c>ctorArgs</c>.
/// </para>
/// <example>
/// <code>
/// [AgentGroupChatMember("code-review")]
/// [AgentTerminationCondition(typeof(KeywordTerminationCondition), "APPROVED")]
/// public class ApprovalAgent { }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AgentTerminationConditionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="AgentTerminationConditionAttribute"/>.
    /// </summary>
    /// <param name="conditionType">
    /// The type that implements <see cref="IWorkflowTerminationCondition"/>. An instance is
    /// created at workflow construction time via
    /// <see cref="Activator.CreateInstance(Type, object[])"/> with <paramref name="ctorArgs"/>.
    /// </param>
    /// <param name="ctorArgs">
    /// Arguments forwarded to the condition's constructor. May be empty if the condition has a
    /// parameterless constructor.
    /// </param>
    public AgentTerminationConditionAttribute(Type conditionType, params object[] ctorArgs)
    {
        ArgumentNullException.ThrowIfNull(conditionType);
        ConditionType = conditionType;
        CtorArgs = ctorArgs ?? [];
    }

    /// <summary>Gets the type of the termination condition to instantiate.</summary>
    public Type ConditionType { get; }

    /// <summary>Gets the constructor arguments forwarded when instantiating the condition.</summary>
    public object[] CtorArgs { get; }
}
