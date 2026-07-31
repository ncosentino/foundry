# Documentation Versus Delivery

**Task:** T128  
**Audited:** 2026-07-30  
**Delivered head:** `08a4fcb20671428245f5004cc0cf7b8acfdcc0a5` on `main`

This audit compares delivered behavior with `README.md`, `docs/maf-harness.md`,
`docs/iterative-agent-loop.md`, API documentation, examples, and release notes.

## Confirmed accurate

| Claim | Source | Delivered behavior |
|---|---|---|
| Alpha/prerelease stability | `README.md`, `docs/maf-harness.md` | Package versions are alpha; no stable promotion occurred |
| Optional bundle is opt-in and non-transitive | `docs/maf-harness.md` | Core `.csproj` has no reference to the bundle or upstream Harness |
| Upstream owns the tool loop and OpenTelemetry for the bundle | `docs/maf-harness.md` | Verified by telemetry-ownership and loop-ownership tests |
| Hybrid context is internal and experimental | `docs/iterative-agent-loop.md`, `docs/maf-harness.md` | Type is `internal`; example prints `Compaction=Disabled` |
| Generated tools with no reflection fallback | `docs/maf-harness.md` | Verified by the NativeAOT Harness application in CI |
| No compatibility shim for former alpha APIs | `CHANGELOG.md`, `docs/maf-harness.md` | No forwarding types exist |
| Shell is a separate opt-in package | `docs/maf-harness.md` | Enforced by `HarnessShellCompositionTests` |
| Comparison is underpowered and decides nothing | `evidence/gate-g8.md`, publication | Every completion interval includes zero |

## Variances

| ID | Severity | Variance | Disposition |
|---|---|---|---|
| D1 | Non-critical | `NexusLabs.Foundry.MicrosoftAgentFramework.Testing.csproj` described only `IAgentScenario` and `AgentScenarioRunner`, omitting the `IHarnessScenario` and `HarnessScenarioRunner` surface delivered by this program. The description was incomplete rather than incorrect: both original types remain public. | Fixed in this gate. |

No critical documentation variance was found.

## Documentation that describes the delivered system

Gate G10 requires documentation to describe the delivered system rather than the
intended system. The delivered documentation satisfies this because:

- it states what is retained rather than promising future removals;
- it labels the optional bundle and testing APIs as prerelease rather than
  stable;
- it labels hybrid compaction as internal and experimental rather than
  supported;
- it discloses that `InternalsVisibleTo` is not a security boundary because the
  assemblies are unsigned; and
- it links the signed retention decision instead of implying a default arm.

## Evidence cross-references

- ADRs 0005-0008 remain accurate and are not superseded by the G8 decision.
- `evidence/gate-g8.md` and `evidence/gate-g9.md` disclose the accepted
  limitations that the public documentation summarizes.
