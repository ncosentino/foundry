# FDRYMAF032: Published tool contract name is blank

## Cause

An `[AIFunctionName]` or `[AIParameterName]` applied to an `[AgentFunction]` declares a null, empty, or whitespace-only name.

## Rule Description

These MEAI attributes replace the C# method or parameter name in the contract exposed to the model. A blank name cannot identify a function or a JSON argument.

The reflection path rejects this while `AIFunctionFactory.Create` builds the function. Foundry's source-generated wrapper enforces the same contract when the generated provider resolves it, and this analyzer moves the failure to the declaration so both paths fail consistently and early.

## How to Fix

Declare a non-blank published name, or remove the naming attribute to use the C# identifier.

### Before

```csharp
[AgentFunction]
[AIFunctionName("")]
public string Search(string query) => "...";
```

### After

```csharp
[AgentFunction]
[AIFunctionName("search")]
public string Search(string query) => "...";
```

The same rule applies to parameters:

```csharp
public string Search(
    [AIParameterName("search_query")] string query) => "...";
```

## When to Suppress

Do not suppress. The reflection path throws on the same declaration, and a source-generated function with no usable name cannot be called reliably.

## See Also

- [FDRYMAF033](FDRYMAF033.md) — published tool contract names collide
- [AI Integrations — Function tools](../ai-integrations.md)
