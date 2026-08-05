namespace NexusLabs.Foundry.MicrosoftAgentFramework;

/// <summary>
/// Marks a method as an agent function that can be auto-discovered by Foundry
/// and registered as an <see cref="Microsoft.Extensions.AI.AIFunction"/> tool
/// for Microsoft Agent Framework agents.
/// </summary>
/// <remarks>
/// Apply this attribute to public methods on a class or static class.
/// Foundry's scanners and source generator will discover all classes that
/// contain at least one method decorated with <c>[AgentFunction]</c> and
/// register them with the agent factory.
///
/// Use <see cref="System.ComponentModel.DescriptionAttribute"/> to provide
/// LLM-friendly descriptions for methods and parameters.
///
/// Use <c>[AIFunctionName("published_name")]</c> and
/// <c>[AIParameterName("published_parameter")]</c> from Microsoft.Extensions.AI when the tool
/// contract should not follow the C# method and parameter names. Foundry honors those attributes
/// identically in its source-generated and reflection paths. They are experimental MEAI APIs and
/// therefore produce the upstream <c>MEAI001</c> diagnostic at the declaration site.
/// </remarks>
/// <example>
/// <code>
/// public class OrderTools
/// {
///     [AgentFunction]
///     [AIFunctionName("get_order_status")]
///     [Description("Look up the status of an order by its ID")]
///     public string GetOrderStatus(
///         [AIParameterName("order_id")]
///         [Description("The order identifier")] string orderId)
///     {
///         return orderId == "123" ? "Shipped" : "Processing";
///     }
/// }
///
/// // Wire the function group to an agent:
/// [FoundryAgent(Instructions = "Help users track their orders")]
/// [AgentFunctionGroup(typeof(OrderTools))]
/// public class OrderTrackingAgent { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AgentFunctionAttribute : Attribute;
