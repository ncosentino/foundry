# G1 Pre-Uplift NativeAOT Baseline

## Baseline identity

- Repository commit: `06b04e6daec39c4a1fb57c3c94e7189fd7803ea0`
- CI branch commit: `2e1c96d3b54e455a6aa2f7d584c3248d29f176c1`
- CI merge tree: `d92417c66be90e7f05de0d447939a7b64ebdf5e4`
- Project:
  `src/Examples/AgentFramework/AotAgentFrameworkApp/AotAgentFrameworkApp.csproj`
- Hosted job:
  [CI run 30036664470, AOT](https://github.com/ncosentino/foundry/actions/runs/30036664470/job/89306185568)
- Runtime: `linux-x64`

## Project constraints

- `PublishAot=true`
- `IsAotCompatible=true`
- `PublishTrimmed=true`
- `TrimMode=full`
- `IlcDisableReflection=true`
- AOT and trim analyzers enabled
- `IL2026`, `IL2060`, `IL2072`, `IL2075`, `IL2091`, and `IL3050` are errors

## Hosted result

```text
dotnet publish src/Examples/AgentFramework/AotAgentFrameworkApp/AotAgentFrameworkApp.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained \
  --output artifacts/aot
```

- Publish: passed
- Native code generation: passed
- IL trim/AOT warnings: none
- Other warnings: one existing generated-code `CS0162` unreachable-code warning
- Output verification: passed as a stripped x86-64 ELF executable
- Uploaded artifact: `foundry-aot-linux-x64`, artifact ID `8575572663`
- Uploaded ZIP SHA-256:
  `a2c018aa685a02167aab3c32fb0d6c3ef6130c71cdb34d5b87a1661cade6e11e`
- Native executable size: 9,505,200 bytes
- Native executable SHA-256:
  `4ddb7b6cd781a7a4c0b13c9efb76fb16b7ec9d07c64ef855ea404ee5cf58b41f`

## Baseline limitation

The current workflow verifies the native executable exists and inspects its file
type, but does not execute it. T010 therefore requires the candidate generated
Harness AOT probe to both publish and execute; file existence alone is not
sufficient evidence. Candidate validation must also republish the unchanged
`AotAgentFrameworkApp` under the uplifted graph so regressions in the existing
NativeAOT path are separated from failures specific to the new Harness probe.
