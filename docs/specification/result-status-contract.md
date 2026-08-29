---
task: APV-1.3
status: approved-baseline
specification: Agent Plugins 1.0.0
verified: 2026-08-23
---

# APV Failure-Boundary and Result-Status Contract

## Purpose

This contract defines how APV V0 turns deterministic findings into an explainable report without discarding the Agent Plugins 1.0.0 failure boundaries.

The [normative rule matrix](rule-matrix.md) is authoritative for rule-level behavior. This document defines APV's product-level aggregation and presentation policy; it does not replace the specification.

## Status model

### Overall result

| Overall status | Meaning | Exit code |
| --- | --- | --- |
| `VALID` | The input claims portable Agent Plugins 1.0.0, its manifest is accepted, and no APV `ERROR` finding remains in any present, supported component. `WARNING` and `INFO` findings do not prevent `VALID`. | `0` |
| `INVALID` | The input claims or presents itself as a portable package and has one or more `ERROR` findings. This includes fatal manifest errors and isolated component/entry errors. Independent valid components remain reported and are not discarded. | `1` |
| `NOT_APPLICABLE` | APV recognizes a vendor-only/non-portable package format and no canonical portable Agent Plugins 1.0.0 root manifest is declared. This is not a conformance failure. | `3` |

`PARTIAL` is intentionally **not** an overall result in V0. It is a component status defined below. This avoids presenting a package with an unresolved conformance error as generally successful while still preserving independently valid skills or MCP entries.

### Component status

| Component status | Meaning |
| --- | --- |
| `VALID` | The present component was fully evaluated and has no `ERROR` findings. An empty but valid `mcpServers` object is valid. |
| `INVALID` | The component was present but is unusable as a component: its top-level configuration failed, or every discovered entry failed. |
| `PARTIAL` | The component has at least one valid entry and at least one invalid entry. Only the invalid entries are skipped. |
| `NOT_EVALUATED` | A fatal manifest error prevented discovery. This is a display state, not a validation result or an additional conformance failure. |
| `ABSENT` | An optional fixed component location was absent. This is a display state, not a validation result or a finding. |

`Manifest` has only `VALID`, `INVALID`, or `NOT_EVALUATED`. `Skills` and `MCP` may use all component display states above where applicable.

### Non-conformance severities

| Finding level | Aggregation effect |
| --- | --- |
| `ERROR` | Makes the overall portable package `INVALID`; use the rule’s narrowest boundary. |
| `WARNING` | Does not prevent `VALID`. In V0 it is used only for explicit specification-defined non-fatal exceptions, such as unknown manifest fields or a non-object `extensions` field. |
| `INFO` | Does not prevent `VALID`. It conveys applicability, guidance, or a non-error condition. |

APV does not use a compatibility percentage, weighted score, or “mostly valid” overall label.

## Applicability and initial decision tree

```text
explicit local directory
        |
        +-- recognized vendor-only format and no portable schema claim
        |       → Overall NOT_APPLICABLE; APV-FORMAT-001 INFO; do not validate vendor format
        |
        +-- root plugin.json with an explicit $schema, missing, unreadable, or malformed
                → portable package candidate
                → validate root manifest
```

- A known Codex-, Claude-, or legacy OpenPlugin marker, or a valid root Copilot manifest without `$schema`, is `NOT_APPLICABLE`. APV does not parse the vendor format.
- A directory expected to be a portable package but missing root `plugin.json`, or containing malformed/nonconforming `plugin.json`, is `INVALID` under the applicable package/manifest rule.
- A recognized but unsupported portable Agent Plugins schema version is `INVALID`, not `NOT_APPLICABLE`, because it is still a portable-format conformance claim outside APV V0 support.
- Invocation errors—such as no argument, an unreadable supplied directory, or invalid CLI option syntax—are **not** package conformance results. They return exit code `2` and a controlled input/usage error.

## Failure-boundary contract

| Trigger | APV boundary | Report behavior | Overall effect |
| --- | --- | --- | --- |
| Root manifest path escapes root, is absent, unreadable, invalid JSON/object, has fatal field/schema violation, or has unsupported portable version | **Plugin** | Manifest `INVALID`; Skills and MCP `NOT_EVALUATED`; do not discover components. | `INVALID` |
| Unknown manifest top-level field | **Continue** | Manifest remains `VALID`; emit `APV-MANIFEST-006` warning and ignore the field. | May remain `VALID` |
| Non-object manifest `extensions` | **Continue** | Manifest remains `VALID`; emit `APV-MANIFEST-007` warning and ignore `extensions`. | May remain `VALID` |
| Present `skills/` has wrong filesystem kind or fixed skills location escapes root | **Skills component** | Skills `INVALID`; MCP still evaluated when manifest is valid. | `INVALID` |
| One discovered skill is invalid or its `SKILL.md` escapes root | **Skill entry** | Skip only that skill; other skills and MCP continue. Skills are `PARTIAL` when at least one sibling is valid, otherwise `INVALID`. | `INVALID` |
| Present `mcp.json` has wrong filesystem kind, invalid JSON/top-level shape/schema, or schema-version mismatch | **MCP component** | MCP `INVALID`/disabled; Skills still evaluated when manifest is valid. | `INVALID` |
| One MCP server entry violates an individual configuration or containment rule | **MCP server entry** | Skip only that server; other servers and Skills continue. MCP is `PARTIAL` when at least one sibling is valid, otherwise `INVALID`. | `INVALID` |
| An optional `skills/` or `mcp.json` location is absent | **No failure** | Report component `ABSENT`; do not create a finding. | May remain `VALID` |
| An unimplemented/unknown client extension namespace exists | **No portable validation** | Ignore its contents; do not create a vendor-validation failure. | May remain `VALID` |

## Aggregation algorithm

1. Determine applicability without interpreting a known vendor-only format as portable conformance failure.
2. If `NOT_APPLICABLE`, produce only the applicability report and stop portable validation.
3. Validate root manifest. If it is fatal, set overall `INVALID`, mark dependent components `NOT_EVALUATED`, and stop discovery.
4. When the manifest is valid, evaluate each present fixed component independently.
5. For Skills and MCP, compute `VALID` / `PARTIAL` / `INVALID` from their own top-level and entry-level findings. Missing optional locations are `ABSENT`.
6. Set overall `INVALID` if any `ERROR` exists; otherwise set `VALID`.
7. Preserve every finding, component count, file path, and rule ID in the report. Do not replace the underlying status with a score.

## Required report fields

Every report must include:

- `overallStatus`: `VALID`, `INVALID`, or `NOT_APPLICABLE`;
- `manifestStatus`, `skillsStatus`, and `mcpStatus`, including `ABSENT`/`NOT_EVALUATED` display states where applicable;
- valid/invalid entry counts for Skills and MCP;
- error, warning, and info counts;
- all findings with Rule ID, level, component, location when available, explanation, suggested fix, and specification reference;
- an applicability message for `NOT_APPLICABLE` that states APV did not validate the vendor format.

The report must not emit a compatibility percentage or a security verdict.

## Canonical examples

| Scenario | Overall | Manifest | Skills | MCP | Reason |
| --- | --- | --- | --- | --- | --- |
| Minimal valid root manifest; no optional components | `VALID` | `VALID` | `ABSENT` | `ABSENT` | Optional locations are not errors. |
| Valid manifest with unknown top-level field only | `VALID` | `VALID` + warning | `ABSENT` | `ABSENT` | The specification says to report and ignore that field. |
| Fatal missing/invalid manifest `$schema` | `INVALID` | `INVALID` | `NOT_EVALUATED` | `NOT_EVALUATED` | Component discovery is prohibited. |
| Valid manifest, one valid skill, one invalid skill, no MCP | `INVALID` | `VALID` | `PARTIAL` | `ABSENT` | The valid skill is preserved, but the package still has an error. |
| Valid manifest and skills; invalid top-level `mcp.json` | `INVALID` | `VALID` | `VALID` | `INVALID` | MCP is disabled; Skills remain valid and visible. |
| Valid manifest, two MCP servers with one invalid entry | `INVALID` | `VALID` | `ABSENT` | `PARTIAL` | Only the invalid server is skipped. |
| Codex-only package with no portable root manifest | `NOT_APPLICABLE` | `NOT_EVALUATED` | `NOT_EVALUATED` | `NOT_EVALUATED` | Vendor package was recognized, not judged against portable rules. |

## Deferred implementation work

- APV-4 implements format detection and fatal manifest handling.
- APV-5 implements skill entry counts and component aggregation.
- APV-6 implements MCP component/entry aggregation and cross-file version boundaries.
- APV-7 implements the public report shape and CLI exit-code behavior.

Until those tasks are complete, this contract is the approved product behavior, not a claim that the CLI already enforces it.
