# FDRYMAF014: FunctionTypes entry has no agent functions

## Cause

A type listed in `FoundryAgentAttribute.FunctionTypes` contains no methods
decorated with `[AgentFunction]`.

## Why it matters

The agent receives zero tools from that type, which can silently remove
capabilities expected by its instructions.

## How to fix

Add at least one `[AgentFunction]` method to the type or remove the type from
`FunctionTypes`.

```csharp
public sealed class CatalogFunctions
{
    [AgentFunction]
    [Description("Looks up a product by identifier.")]
    public Product GetProduct(
        [Description("The product identifier.")] string id) =>
        ...;
}
```
