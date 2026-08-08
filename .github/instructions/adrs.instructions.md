---
applyTo: "docs/adr/*.md"
---

# Architecture decision records

- Use `adr-NNNN-title-slug.md` and the repository's established frontmatter.
- Preserve accepted decision reasoning. Supersede a material decision with a new
  ADR and link both lifecycle fields instead of rewriting history.
- Record system structure, integration boundaries, important quality
  attributes, or other costly-to-reverse choices; do not create ADRs for routine
  implementation details.
- Add every ADR to the Architecture Decisions section of `mkdocs.yml`.
