# FDRYMAF013: Agent function parameter is missing a description

## Cause

A parameter on an `[AgentFunction]` method does not have a
`[System.ComponentModel.Description]` attribute. `CancellationToken`
parameters are exempt.

## Why it matters

Parameter descriptions become part of the generated tool schema and tell the
model what value to supply.

## How to fix

Describe each model-supplied parameter.

```csharp
[AgentFunction]
[Description("Searches the catalog.")]
public SearchResult Search(
    [Description("The product name or phrase to search for.")] string query) =>
    ...;
```
