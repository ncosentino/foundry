---
applyTo: "README.md,CHANGELOG.md,mkdocs.yml,docs/**/*,requirements-docs.txt,defaultdocumentation.json,scripts/**/*docs*,scripts/{api_package_index,build-sitemap-index,generate-api-catalog,merge-docs-site,trim-cloudflare-mirror}.py,scripts/tests/**/*.py"
---

# Documentation

- `mkdocs.yml` is the canonical documentation map. Every maintained Markdown
  page under `docs/` must remain reachable from its navigation, including ADRs.
- Documentation states current truth or an explicit target state. Keep rollout
  chronology in git, issues, pull requests, or the changelog.
- Accepted ADR reasoning is historical decision evidence and is governed by the
  ADR-specific contract rather than rewritten as current-state documentation.
- Update links and generated-documentation inputs at their owning source; do not
  hand-maintain workflow-generated API output.
- Public documentation must not disclose local paths, private repositories,
  internal services, credentials, or private operational context.
- Preserve the strict MkDocs build and documentation-script tests declared by
  the documentation workflow.
