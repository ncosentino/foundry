# Traceability Matrix: First-Class Microsoft Agent Framework Harness Support

This matrix maps reviewed requirements and success criteria to planned task
groups. It supplements story labels for foundational and cross-cutting tasks.

## Functional Requirements

| Requirement | Primary tasks |
|---|---|
| FR-001 | T026, T077 |
| FR-002 | T005-T006, T026, T094 |
| FR-003 | T007-T009, T021, T073, T093 |
| FR-004 | T016, T024, T075, T081, T084-T085 |
| FR-005 | T032-T040 |
| FR-006 | T072-T080 |
| FR-007 | T015, T023, T071, T074, T087 |
| FR-008 | T001-T014 |
| FR-009 | T005-T006 |
| FR-010 | T011-T012, T040, T071 |
| FR-011 | T029-T031, T057-T071 |
| FR-012 | T059, T064, T067, T070-T071 |
| FR-013 | T057-T058, T065, T070 |
| FR-014 | T041, T045, T051 |
| FR-015 | T045-T046, T050-T051 |
| FR-016 | T047, T052-T053 |
| FR-017 | T047, T053 |
| FR-018 | T054-T055, T068-T069 |
| FR-019 | T048, T060-T063 |
| FR-020 | T020, T042-T043, T049, T056 |
| FR-021 | T020, T042-T049, T056 |
| FR-022 | T020-T021, T025, T044, T048 |
| FR-023 | T029-T031 |
| FR-024 | T029, T034-T035 |
| FR-025 | T034-T036 |
| FR-026 | T029-T040 |
| FR-027 | T015, T037, T057, T067, T071, T074 |
| FR-028 | T039-T040 |
| FR-029 | T018, T027, T076, T079 |
| FR-030 | T019, T027, T076, T079 |
| FR-031 | T036, T054-T055, T068-T069, T097, T100 |
| FR-032 | T048, T063, T100 |
| FR-033 | T015-T021, T029, T032, T034, T037, T042-T048, T057-T063 |
| FR-034 | T095-T121 |
| FR-035 | T098-T120 |
| FR-036 | T004, T008, T010, T081, T084-T090 |
| FR-037 | T121, T125, T129 |
| FR-038 | T028, T056, T071, T080, T121, T125, T131 |
| FR-039 | T091-T094, T126, T128, T131 |
| FR-040 | T046, T053 |
| FR-041 | T060, T065-T066 |
| FR-042 | T057, T065 |
| FR-043 | T057, T066 |
| FR-044 | T095, T106-T107, T113, T119-T120 |
| FR-045 | T096, T098-T107 |
| FR-046 | T103-T105, T115-T116, T120-T121 |
| FR-047 | T022-T028, T087-T090, T124 |
| FR-048 | T020-T021, T037, T044, T048, T091-T093 |
| FR-049 | T060-T061, T066, T070 |
| FR-050 | T055, T069, T101, T111 |
| FR-051 | T045, T047, T051, T099, T109 |
| FR-052 | T017-T019, T027, T041, T062, T070, T076 |
| FR-053 | T015, T023, T071, T074, T080 |
| FR-054 | T005-T006 |
| FR-055 | T010, T081, T084, T086, T094 |
| FR-056 | T028, T056, T080, T121, T125, T127, T129-T131 |
| FR-057 | T095, T102, T106, T112-T120 |
| FR-058 | T095-T120 |
| FR-059 | T095, T117-T121 |
| FR-060 | T122, T126 |

## Success Criteria

| Success criterion | Primary tasks |
|---|---|
| SC-001 | T015-T028, T094 |
| SC-002 | T057-T071 |
| SC-003 | T045-T046, T050-T051, T099, T109 |
| SC-004 | T057-T058, T065 |
| SC-005 | T047, T052-T053, T099, T109 |
| SC-006 | T021, T044, T048, T100 |
| SC-007 | T019, T076, T097, T100 |
| SC-008 | T001, T005-T014 |
| SC-009 | T002, T007-T009, T014 |
| SC-010 | T095-T121 |
| SC-011 | T121, T125, T127-T131 |
| SC-012 | `reviews/spec-review.md`, `reviews/plan-review.md`, `reviews/final-analysis.md`, T123 |
| SC-013 | T057, T060, T065-T066 |
| SC-014 | T045, T051, T099, T109 |
| SC-015 | T120 |
| SC-016 | T048, T063, T100 |
| SC-017 | T010, T081, T084, T086 |
| SC-018 | T071, T080, T087, T094 |

## Gate Traceability

| Gate | Evidence tasks |
|---|---|
| G0 Planning closure | Current reviewed Spec Kit artifacts and final analysis |
| G1 Package compatibility | T001-T014 |
| G2 Composition foundation | T015-T028 |
| G3 Stable provider slices | T029-T040 |
| G4 Workspace/offload | T041-T056 |
| G5 Experimental hybrid | T057-T071 |
| G6 Optional bundle | T072-T080 |
| G7 Hardening | T081-T094 |
| G8 Hosted comparison | T095-T121 |
| G9 Final integration/release | T122-T126 |
| G10 Reconciliation/cleanup | T127-T131 |

## Explicit Cross-Cutting Task Mapping

These tasks support multiple requirements and are listed explicitly so no
operational task is left without traceability.

| Task | Requirement/success mapping |
|---|---|
| T003 | FR-030, FR-031, SC-007 |
| T013 | FR-008, FR-052, FR-054, SC-008 |
| T030 | FR-023, FR-026 |
| T033 | FR-026 |
| T038 | FR-026, FR-048 |
| T078 | FR-004, FR-006 |
| T082 | FR-033 |
| T083 | FR-033 |
| T088 | FR-047, FR-055 |
| T089 | FR-047, FR-055 |
| T092 | FR-039 |
| T104 | FR-046 |
| T108 | FR-035, FR-058 |
| T110 | FR-035, FR-058 |
| T114 | FR-057, FR-058 |
| T118 | FR-059 |
| T127 | FR-056, SC-011 |
| T128 | FR-039, SC-011 |
| T129 | FR-037, FR-056, SC-011 |
| T130 | FR-056, SC-011 |
| T131 | FR-038, FR-039, FR-056, SC-011 |
