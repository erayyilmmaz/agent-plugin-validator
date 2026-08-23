---
task: APV-2.3
status: approved-contract
specification: Agent Plugins 1.0.0
verified: 2026-08-23
---

# APV Rule Registry and Specification-Reference Contract

## Purpose

This document defines the single-rule-registry approach that turns the stable
APV Rule IDs into safe, reproducible finding metadata. It completes the model
contract in [validation-model.md](../architecture/validation-model.md) without
creating a Core project, choosing serialization, or fetching a schema or
specification at validation time.

The registry is the Core's authoritative lookup for an emitted finding. The
[Rule ID dictionary](rule-id-dictionary.md) remains the stable public ID
catalogue, and the [normative rule matrix](rule-matrix.md) remains the
human-readable normative rationale and failure-boundary source. They must stay
in lockstep with the registry.

## Ownership and source of truth

```text
Rule ID dictionary + normative rule matrix
                  |
                  v
       immutable Core RuleRegistry (future)
                  |
                  v
        RuleDefinition + source reference(s)
                  |
                  v
               Finding
```

The future `AgentPluginValidator.Core.Contracts` model owns `RuleDefinition`,
`SpecificationSource`, and `SpecificationReference`. The future validation
logic asks the registry for a known rule and supplies observation-specific
location, explanation, and safe values. It cannot invent a new ID, severity,
component ownership, or source URL at runtime.

The public markdown files are design and review evidence; the later Core must
use an explicit immutable in-process registry rather than parsing markdown or
downloading remote content. This keeps validation deterministic and preserves
the no-network/no-execution boundary.

## Target registry contracts

The following are target type names only. No C# types are created by this task.

| Contract | Required fields | Responsibility |
| --- | --- | --- |
| `RuleId` | `Value` | Validated stable identifier in the form already reserved by the dictionary. |
| `RuleDefinition` | `RuleId`, `Title`, `DefaultLevel`, `DefaultComponent`, `FailureBoundary`, `References` | Immutable rule metadata used whenever a finding is emitted. |
| `SpecificationSource` | `SourceId`, `Authority`, `Title`, `VersionOrSnapshot`, `CanonicalLocator` | Registry-owned identity for one normative specification or APV product contract. |
| `SpecificationReference` | `SourceId`, `Locator`, `DisplayLabel` | A stable, renderable pointer into a `SpecificationSource`; one finding exposes one or more resolved references. |
| `RuleRegistry` | `GetRequired(RuleId)`, `TryGet(RuleId)` | Read-only lookup. It is constructed once from the complete known definition list. |

`DefaultComponent` is the default owner, not an excuse to erase a narrower
entry owner: a rule used for an individual skill or MCP server is emitted with
`FindingComponent.Skill` or `FindingComponent.McpServer` as defined by the
model contract. `FailureBoundary` is the declared semantic boundary from the
matrix; the implementation still decides the concrete local context for
`APV-PATH-001` and `APV-PATH-002`.

`RuleDefinition.References` is non-empty. A rule can cite more than one
section when the conformance obligation spans both a general rule and a
component-specific rule.

## Source catalogue and reference resolution

The source catalogue is finite and registry-owned:

| Source ID | Authority | Title / version or snapshot | Canonical locator |
| --- | --- | --- | --- |
| `APV-SPEC-PLUGIN-1.0.0` | Normative specification | Agent Plugins Specification 1.0.0 | `https://github.com/agentplugins/agent-plugins-spec/blob/main/spec/1.0.0.md` |
| `APV-SPEC-SKILLS` | Normative specification | Agent Skills Specification, checked 2026-08-23 | `https://agentskills.io/specification` |
| `APV-POLICY-RESULT-STATUS-V0` | APV product contract | APV Failure-Boundary and Result-Status Contract V0 | `apv://policy/result-status-contract` |

`CanonicalLocator` is an official URL for an external specification and a
stable APV document URI for an internal product policy. A rendered finding
must show the source title, version/snapshot, locator, and canonical locator.
It may render the policy URI as a repository document link; it must label it
as an APV policy, not as an external normative specification.

Reference resolution is local:

1. A rule definition stores only `SourceId`, `Locator`, and `DisplayLabel`.
2. The registry resolves `SourceId` against the finite source catalogue.
3. The report copies the resolved, immutable metadata into its
   `SpecificationReference` field.
4. The validator never dereferences the URL/URI, follows redirects, reads a
   local document dynamically, or accepts a package-supplied reference.

This preserves reproducible results even when an external page later changes.
The `VersionOrSnapshot` explains the exact authority APV V0 was designed
against; it does not claim that the validator retrieved that version during a
run.

## Complete V0 rule-reference map

The following table is the APV-2.3 registry baseline. A semicolon denotes
multiple references owned by one rule.

| Rule ID | Default level | Default owner | Failure boundary | Reference key(s) |
| --- | --- | --- | --- | --- |
| `APV-PACKAGE-001` | Error | Package | Plugin | `APV-SPEC-PLUGIN-1.0.0 §4.1.1–2; §5.1` |
| `APV-PATH-001` | Error | Package | Context-specific | `APV-SPEC-PLUGIN-1.0.0 §4.1.3; §4.1 failure list` |
| `APV-PATH-002` | Error | Package | Context-specific | `APV-SPEC-PLUGIN-1.0.0 §4.1.4–5` |
| `APV-MANIFEST-001` | Error | Manifest | Plugin | `APV-SPEC-PLUGIN-1.0.0 §5.2` |
| `APV-MANIFEST-002` | Error | Manifest | Plugin | `APV-SPEC-PLUGIN-1.0.0 §5.2–§5.3` |
| `APV-MANIFEST-003` | Error | Manifest | Plugin | `APV-SPEC-PLUGIN-1.0.0 §5.3` |
| `APV-MANIFEST-004` | Error | Manifest | Plugin | `APV-SPEC-PLUGIN-1.0.0 §5.5` |
| `APV-MANIFEST-005` | Error | Manifest | Plugin | `APV-SPEC-PLUGIN-1.0.0 §5.2; §5.4` |
| `APV-MANIFEST-006` | Warning | Manifest | Continue | `APV-SPEC-PLUGIN-1.0.0 §5.2` |
| `APV-MANIFEST-007` | Warning | Manifest | Continue | `APV-SPEC-PLUGIN-1.0.0 §5.2; §8.1` |
| `APV-COMPONENT-001` | Error | Skills | Component | `APV-SPEC-PLUGIN-1.0.0 §6.1–§6.2` |
| `APV-COMPONENT-002` | Error | Mcp | Component | `APV-SPEC-PLUGIN-1.0.0 §6.1–§6.2; §7.2.1` |
| `APV-SKILL-001` | Error | Skill | Entry | `APV-SPEC-PLUGIN-1.0.0 §4.1; §7.1` |
| `APV-SKILL-002` | Error | Skill | Entry | `APV-SPEC-SKILLS SKILL.md format` |
| `APV-SKILL-003` | Error | Skill | Entry | `APV-SPEC-SKILLS name field` |
| `APV-SKILL-004` | Error | Skill | Entry | `APV-SPEC-SKILLS frontmatter, name field` |
| `APV-SKILL-005` | Error | Skill | Entry | `APV-SPEC-SKILLS frontmatter, description field` |
| `APV-MCP-001` | Error | Mcp | Component | `APV-SPEC-PLUGIN-1.0.0 §7.2.1–§7.2.2` |
| `APV-MCP-002` | Error | Mcp | Component | `APV-SPEC-PLUGIN-1.0.0 §7.2.1–§7.2.2` |
| `APV-CROSS-001` | Error | Mcp | Component | `APV-SPEC-PLUGIN-1.0.0 §7.2.2; §10.1` |
| `APV-MCP-003` | Error | McpServer | Entry | `APV-SPEC-PLUGIN-1.0.0 §7.2.1` |
| `APV-MCP-010` | Error | McpServer | Entry | `APV-SPEC-PLUGIN-1.0.0 §7.2.1` |
| `APV-MCP-011` | Error | McpServer | Entry | `APV-SPEC-PLUGIN-1.0.0 §7.2.1` |
| `APV-MCP-012` | Error | McpServer | Entry | `APV-SPEC-PLUGIN-1.0.0 §7.2.1; §9.2` |
| `APV-MCP-013` | Error | McpServer | Entry | `APV-SPEC-PLUGIN-1.0.0 §9.2` |
| `APV-MCP-020` | Error | McpServer | Entry | `APV-SPEC-PLUGIN-1.0.0 §7.2.1` |
| `APV-MCP-021` | Error | McpServer | Entry | `APV-SPEC-PLUGIN-1.0.0 §7.2.1` |
| `APV-MCP-022` | Error | McpServer | Entry | `APV-SPEC-PLUGIN-1.0.0 §7.2.1` |
| `APV-MCP-023` | Error | McpServer | Entry | `APV-SPEC-PLUGIN-1.0.0 §9.2` |
| `APV-FORMAT-001` | Info | Package | Not applicable | `APV-POLICY-RESULT-STATUS-V0 Applicability and initial decision tree` |

The `Default owner` column does not replace the report's actual finding owner
where context is narrower. It makes registry metadata reviewable and lets a
future Core reject an impossible component/rule pairing.

## Registry invariants and change control

- Every reserved V0 Rule ID appears exactly once in the registry baseline.
- A registry Rule ID must appear exactly once in the Rule ID dictionary and
  exactly once in the normative rule matrix.
- Default level and declared failure boundary must agree with both documents.
- Every rule has one or more known `SpecificationReference` values; no raw URL
  or package-provided text can become a report reference.
- Rule IDs are append-only. A retired rule remains resolvable with its original
  meaning and is marked deprecated rather than renamed or reused.
- Adding/changing a rule requires one reviewable change set covering the
  dictionary, matrix, registry mapping, source catalogue where necessary,
  fixtures/tests when they exist, and vault decision/log.

The first Core implementation must add registry-focused tests for duplicate IDs,
unknown lookup, source-ID resolution, non-empty references, dictionary/matrix
parity, fixed source metadata, and deterministic reference ordering. Those are
future tests, not claims that a project or test runner now exists.

## Deferred work

- APV-3 defines input/path reading and cannot add package-controlled references
  to a finding.
- APV-4 through APV-6 use only known registry entries while implementing
  package, manifest, Skills, and MCP validation.
- APV-7 selects the public JSON/text rendering while preserving the resolved
  reference data without remote fetches.
- Actual C# contracts, generated documentation, serialization, and test
  projects remain uncreated until their assigned implementation tasks.
