; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
FDRYMAF031 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | DuplicateAgentNameAnalyzer, Two declared agents publish the same name
FDRYMAF032 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | AgentFunctionPublishedNameAnalyzer, Published tool contract name is blank
FDRYMAF033 | NexusLabs.Foundry.MicrosoftAgentFramework | Error | AgentFunctionPublishedNameAnalyzer, Published tool contract names collide
