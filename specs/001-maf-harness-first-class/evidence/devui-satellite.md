# G1 DevUI Satellite Compatibility

## Candidate

- `Microsoft.Agents.AI.DevUI`
  `1.15.0-preview.260722.1`
- Project:
  `src/NexusLabs.Foundry.MicrosoftAgentFramework.DevUI/NexusLabs.Foundry.MicrosoftAgentFramework.DevUI.csproj`
- Example:
  `src/Examples/AgentFramework/DevUIApp/DevUIApp.csproj`

## Evidence

```powershell
dotnet build src\NexusLabs.Foundry.MicrosoftAgentFramework.DevUI\NexusLabs.Foundry.MicrosoftAgentFramework.DevUI.csproj --configuration Release --no-restore --nologo
dotnet build src\Examples\AgentFramework\DevUIApp\DevUIApp.csproj --configuration Release --no-restore --nologo
```

- Integration package: passed with zero warnings and zero errors.
- Example application: passed with zero errors and two existing generated-code
  `CS0162` warnings.
- Existing `AddDevUI()` and `MapDevUI()` usage remains source-compatible.
- PR #71 hosted build, test, and pack passed on the candidate graph.

## Disposition

Compile and package compatibility: **passed**.

Interactive browser/runtime smoke: **deferred**. The existing example requires
an authenticated Copilot chat client and a running web host; G1 does not change
that runtime path or make the preview satellite part of the core Harness gate.

