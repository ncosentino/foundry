# FDRYMAF012: Agent function is missing a description

## Cause

A method decorated with `[AgentFunction]` does not have a
`[System.ComponentModel.Description]` attribute.

## Why it matters

The model uses the description to decide when to call a tool. An unlabeled
function is more likely to be ignored or invoked incorrectly.

## How to fix

Add a concise description that explains the function's intent and result.

```csharp
[AgentFunction]
[Description("Returns the current weather for a city.")]
public WeatherResult GetWeather(string city) => ...;
```
