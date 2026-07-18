# FDRYMAF015: Do not call ToString on tool result objects

## Cause

Code calls `.ToString()` on a tool result property such as
`ToolCallResult.Result` or `FunctionResultContent.Result`.

## Why it matters

The value may be a `JsonElement`. Calling `.ToString()` can lose the original
JSON representation for arrays and complex objects.

## How to fix

Use `ToolResultSerializer.Serialize()` to produce a stable string
representation.

```csharp
var text = ToolResultSerializer.Serialize(toolResult.Result);
```
