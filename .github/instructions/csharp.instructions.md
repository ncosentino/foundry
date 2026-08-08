---
applyTo: "src/**/*.cs"
---

# C# source

- Keep one type per file and use file-scoped namespaces.
- Use `internal` unless consumers directly reference the type.
- Use records for data carriers and classes for services or mutable behavior.
- Document every public type and member with XML documentation.
- Prefer interfaces and composition over inheritance and static state.
- Do not add optional parameters or default interface members. Use explicit
  overloads and required options records.
- New diagnostic IDs use the `FDRY` prefix followed by the component code.
