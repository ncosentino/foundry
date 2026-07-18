---
title: "ADR-0004: Extract the AI platform from Needlr into Foundry"
status: "Accepted"
date: "2026-07-17"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "repository", "packages", "needlr", "foundry"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Needlr began as an opinionated dependency-injection, plugin, and source-generation
framework. Its public identity, documentation, package metadata, and core architecture
continue to describe that purpose.

The repository later accumulated a separate AI and agentic platform covering Microsoft
Agent Framework orchestration, Microsoft.Extensions.AI evaluation, experiment execution,
MEAI Reporting, Langfuse, GitHub Copilot, Semantic Kernel, testing, diagnostics, progress,
workspace abstractions, source generation, and analyzers.

At the time of this decision, AI production projects contain approximately half of the
repository's production C# lines and dominate recent development activity. The repository
uses one version definition and one release workflow for both the DI framework and AI
packages. Consequently, provider updates and preview dependencies cause unrelated Needlr
packages to share the same release cadence and dependency posture.

The production dependency direction is already one-way: AI projects depend on Needlr,
while non-AI Needlr production projects do not depend on the AI projects. The Copilot
package has no Needlr dependency beyond its package and namespace identity. Evaluation,
Reporting, and Langfuse primarily depend on AI diagnostics and experiment abstractions;
their direct Needlr coupling is largely registration metadata.

This decision governs repository ownership, package identity, dependency direction,
source-generation integration, and release ownership for the AI platform. It does not
redesign the behavior selected by the existing workflow, testing, or experiment ADRs.

## Decision drivers

- Needlr must retain a coherent identity as a dependency-injection and plugin framework.
- AI runtime and provider packages need an independent release cadence from Needlr.
- Neutral AI capabilities should be usable without adopting Needlr.
- Needlr integration must remain first-class, source-generated, analyzer-backed, and AOT
  compatible.
- Fast-moving provider and preview dependencies must not determine Needlr's audit or
  compatibility posture.
- The migration should establish the intended final architecture rather than preserve
  alpha package compatibility.

## Decision

The AI and agentic platform will be owned by the Foundry repository.

Foundry owns:

- Microsoft Agent Framework runtime and workflow support;
- shared agent diagnostics, progress, budget, context, workspace, and tool abstractions;
- Microsoft.Extensions.AI evaluation and provider-neutral experiment execution;
- MEAI Reporting integration;
- Langfuse telemetry, datasets, traces, scores, and experiment adapters;
- GitHub Copilot and Semantic Kernel integrations;
- deterministic testing support, DevUI integration, examples, documentation, generators,
  analyzers, and the AI-specific architecture records.

Neutral Foundry packages use `NexusLabs.Foundry` package IDs and namespaces and must not
depend on Needlr.

Foundry also owns explicit Needlr integration packages under
`NexusLabs.Foundry.Needlr`. These packages depend on Foundry and Needlr and provide
Syringe integration, source-generated registration, analyzers, and any required
auto-registration filtering. Needlr does not depend on Foundry.

Source generation will separate neutral registry, factory, and workflow emission from
Needlr-specific Syringe extensions. A generator may emit neutral Microsoft DI or Foundry
bootstrap code without Needlr. Needlr-specific generated code belongs to the Foundry
Needlr integration package.

Foundry and Needlr use independent versioning, release pipelines, package metadata,
documentation sites, and dependency policies.

The existing `NexusLabs.Foundry.MicrosoftAgentFramework`, `NexusLabs.Foundry.Copilot`, and
`NexusLabs.Foundry.Needlr.SemanticKernel` alpha package identities will not receive forwarding
packages, compatibility wrappers, or legacy namespaces. The migration may make breaking
API and package changes.

## Alternatives considered

### Keep the AI platform in the Needlr repository

This preserves atomic changes and shared build infrastructure. It was rejected because it
requires two products with different audiences, dependencies, and release cadences to
share one identity and version stream. Continued AI growth would make the mismatch larger.

### Move neutral runtime packages but keep AI-specific Needlr integration in Needlr

This reduces the code remaining in Needlr and keeps integration changes near the DI
framework. It was rejected as the permanent boundary because AI attributes, generators,
analyzers, DevUI support, and provider adaptations evolve with the AI platform. Splitting
their ownership across repositories would require coordinated releases while still
leaving AI-specific implementation in Needlr.

### Move the current projects without changing package or dependency boundaries

This is mechanically simpler and preserves current package names. It was rejected because
it would create a new repository whose neutral packages still carry Needlr branding and
dependencies. That would move files without correcting the architectural problem.

## Consequences

### Positive

- Needlr returns to a focused DI, plugin, and source-generation product.
- Foundry can serve consumers that use Microsoft DI or another composition system.
- MAF, MEAI, Langfuse, Copilot, and Semantic Kernel changes can release independently.
- Preview dependencies and provider-specific operational risks remain isolated in Foundry.
- The Needlr integration exercises the same external plugin model available to other
  framework integrations.

### Negative

- Source projects, package IDs, namespaces, documentation, and examples require a broad
  breaking migration.
- Cross-repository development requires explicit compatibility testing between released
  Needlr packages and Foundry's Needlr integration.
- Shared build, documentation, and release infrastructure must be established separately.
- The current Agent Framework project mixes neutral and Needlr-specific responsibilities
  and must be split before the dependency rule can be enforced.

### Neutral

- Existing workflow, evaluation, provider, and testing semantics remain valid unless a
  separate ADR changes them.
- Existing alpha Needlr packages remain available at their published versions but are not
  maintained as compatibility surfaces.
- Needlr may add generic extension points needed by Foundry, provided those extension
  points are useful to external integrations generally and contain no AI-specific behavior.

## Confirmation

The decision is confirmed when:

- neutral Foundry projects build without any Needlr package or project reference;
- Foundry's Needlr integration consumes released Needlr packages through a one-way
  dependency;
- Needlr builds, tests, and releases without MAF, MEAI, Langfuse, Copilot, or Semantic
  Kernel dependencies;
- Foundry has independent versioning, CI, package publication, and documentation;
- source-generated and analyzer-backed Needlr integration remains functional from the
  external Foundry package;
- representative consumers migrate without retaining a duplicate scheduler, workflow,
  evaluation, or provider-publication layer.

Repository inspection cannot prove provider availability, model quality, or consumer
migration value. Those require hosted integration checks and real consumer migrations.

## References

- ADR-0001 records the graph workflow behavior that moves with the Foundry AI platform.
- ADR-0002 records deterministic agent testing behavior that moves with Foundry.
- ADR-0003 records the provider-neutral experiment and adapter boundaries that move with
  Foundry.
- The Needlr release workflow demonstrates the former single-version, single-release
  coupling by packing every `NexusLabs.Needlr*` package from one tag.
- The former Needlr Agent Framework project demonstrates the mixed boundary by combining
  neutral diagnostics and orchestration with Syringe extensions and bundled generators.
