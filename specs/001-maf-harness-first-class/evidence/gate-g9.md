# Gate G9 Decision — Final Integration and Release Review

## Decision

**PASS for the cumulative G9 release-review slice.**

G9 revalidates the delivered Harness system before any public promotion:

1. shell package-boundary and manual-composition enforcement;
2. a re-run cross-artifact analysis against the implemented profile;
3. a public API and XML-documentation review;
4. release-bound dispositions for every temporary duplication entry; and
5. release notes and migration guidance.

Nothing is promoted to stable in this gate. The optional bundle and Harness
testing APIs remain prerelease.

## Evidence identity

| Task | Artifact | Disposition |
|---|---|---|
| T122 | `src/NexusLabs.Foundry.MicrosoftAgentFramework.Tests/Harness/HarnessShellCompositionTests.cs`, `docs/maf-harness.md` | Complete |
| T123 | `reviews/pre-release-analysis.md` | Complete |
| T124 | `evidence/api-review.md` | Complete |
| T125 | `evidence/duplication-ledger.md` | Complete |
| T126 | `CHANGELOG.md`, `docs/maf-harness.md` | Complete |

Base: `d3168ceb65c86746aae4ba00eff720f469d95823` on `main`, which contains the
merged Gate G8 evidence.

## Gate criteria

### No critical cross-artifact inconsistency

`reviews/pre-release-analysis.md` records four findings — one High, two Medium,
one Low — and **zero critical**. Every finding was resolved inside this gate
rather than deferred:

- P1 (High): FR-060 had no enforcing test and no documentation statement.
- P2 (Medium): `ScenarioRunResult` positional parameters were undocumented.
- P3 (Medium): the duplication ledger had no release-bound disposition artifact.
- P4 (Low): the evaluation jobs still used a floating SDK setup.

### Every public member is intentionally promoted and documented

`evidence/api-review.md` enumerates the public surface by reflection:

| Package | Public types | Undocumented author-written members |
|---|---:|---:|
| `...MicrosoftAgentFramework` | 202 | 0 |
| `...MicrosoftAgentFramework.Harness` | 9 | 0 |
| `...MicrosoftAgentFramework.Testing` | 19 | 0 after the T124 fix |

The core package exposes no public type in a `Harness` namespace; all 176
Harness types there are `internal`, preserving the G6/G7 disposition that
selected-provider composition is an unsupported internal seam. The only
publicly promoted Harness-named types are progress diagnostics events.

### Every temporary duplicate has a retention or deletion disposition

`evidence/duplication-ledger.md` gives all eight entries a release-bound
disposition. DUP-001, DUP-002, DUP-006, and DUP-008 are bound to the signed G8
decision; DUP-003, DUP-004, and DUP-007 were resolved at G2/G7; DUP-005 is a
permanent invariant. No entry is scheduled for deletion in this release.

### Migration and release guidance matches the supported capability matrix

`CHANGELOG.md` and `docs/maf-harness.md` state that no compatibility shim
exists, that adoption is additive, that nothing is deprecated, that
selected-provider composition is internal and unsupported, that shell is a
separate opt-in package, and that the bundle and testing APIs are prerelease.

## Shell disposition

FR-060 is enforced, not merely asserted. `HarnessShellCompositionTests` proves
that `HarnessCapability` exposes no shell toggle, that no `src` project
references a shell package, that `src/Directory.Packages.props` pins no shell
package, and that the documentation records the absent `HarnessAgentOptions`
shell property alongside the manual tool and context-provider composition path.

## Accepted limitations carried into release

- Selected-provider composition remains internal; `InternalsVisibleTo` is not a
  security boundary because the repository assemblies are unsigned.
- Experimental hybrid compaction remains internal and AOT-unverified.
- The hosted comparison is underpowered at eight cases; no default or removal is
  supported.
- The Copilot SDK exposes no raw tool-capable chat-completion API, so hosted
  evaluation supplies transcripts as an identical-per-arm JSON meta-prompt.

## Deferred work

Implementation-versus-plan reconciliation, documentation-versus-delivery audit,
post-MVP follow-up filing, and the specification retention and cleanup decision
remain G10.
