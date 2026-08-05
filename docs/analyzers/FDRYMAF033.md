# FDRYMAF033: Published tool contract names collide

## Cause

Either:

- two `[AgentFunction]` methods in one function type publish the same function name; or
- two parameters in one function publish the same parameter name.

The published name is `[AIFunctionName("...")]` or `[AIParameterName("...")]` when declared, and the C# identifier otherwise. A declared name can therefore collide with another declared name or with an unchanged method or parameter name.

## Rule Description

Function names identify tools in model requests and `FunctionCallContent`. Two functions in one type sharing a name are unconditionally ambiguous whenever that type is used.

Parameter names become JSON object keys in the function schema. MEAI's reflection path rejects duplicate keys while creating the function; Foundry's source-generated path does the same.

The analyzer deliberately does **not** reject same-named functions in separate types. Separate agents may legitimately use those types independently and never resolve both names together. `IAgentFactory` checks the actual tool set at runtime and fails when an agent does resolve a cross-type collision.

## How to Fix

Give one method or parameter a distinct published name:

```csharp
[AgentFunction]
[AIFunctionName("search_support")]
public string SearchSupport(string query) => "...";

[AgentFunction]
[AIFunctionName("search_billing")]
public string SearchBilling(string query) => "...";
```

## When to Suppress

Do not suppress a collision within one type or method. There is no valid invocation that can distinguish the duplicated contract.

A cross-type collision is not reported by this rule; scope each agent to a non-colliding set of `FunctionTypes` or `FunctionGroups`, or rename one function if an agent needs both.

## See Also

- [FDRYMAF032](FDRYMAF032.md) — published tool contract name is blank
- [FDRYMAF031](FDRYMAF031.md) — two declared agents publish the same name
