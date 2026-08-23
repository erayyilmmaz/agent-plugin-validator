---
task: APV-2.2
status: approved-contract
verified: 2026-08-23
---

# APV Validation Finding, Component, and Report Model

## Purpose and scope

This document defines the implementation contract for the data produced by the
validation core. It is deliberately a model design, not a `.NET` scaffold,
public serialization schema, or CLI output design. The target projects remain
the uncreated boundaries described in
[solution-boundaries.md](solution-boundaries.md).

The core owns this model. The CLI may create a request and render a report, but
it must not redefine finding, component, applicability, or status semantics.

The model describes validation of one explicitly selected local package
directory. It is limited to the portable Agent Plugins 1.0.0 conformance
contract and inherits the no-execution invariant from
[never-execute-contract.md](../security/never-execute-contract.md).

## Model ownership

The following namespace is a target naming convention only; it is not created
by this task:

```text
AgentPluginValidator.Core.Contracts
```

```text
Validation request
        |
        v
Validation core
        |
        v
ValidationReport (one package)
  |- format/applicability decision
  |- overall package status
  |- Manifest component result
  |- Skills component result
  |- MCP component result
  |- package, component, and entry findings
  `- derived summary
```

`ValidationReport` is the aggregate root. There is no separate, competing
Manifest, Skill, MCP, or Package report format: all findings and component
results belonging to a package are collected under this one report.

## Enumerations

Names below are target Core contract names. Their concrete C# representation is
deferred until the projects are scaffolded.

| Contract | Values | Meaning |
| --- | --- | --- |
| `FindingLevel` | `Error`, `Warning`, `Info` | A finding's severity. An `Error` makes an applicable portable package `Invalid`; `Warning` and `Info` do not. |
| `FindingComponent` | `Package`, `Manifest`, `Skills`, `Skill`, `Mcp`, `McpServer` | The owned validation surface. `Skill` and `McpServer` identify an individual entry; their parent component is still reported separately. |
| `ComponentKind` | `Manifest`, `Skills`, `Mcp` | The three reportable validation components. |
| `ComponentStatus` | `Valid`, `Invalid`, `Partial`, `NotEvaluated`, `Absent` | Component outcome. `Partial` is component-only and never an overall package status. |
| `OverallStatus` | `Valid`, `Invalid`, `NotApplicable` | Final conformance result for a report. |
| `ApplicabilityDisposition` | `PortableCandidate`, `RecognizedNonPortable`, `Unrecognized` | The format decision used to determine whether portable conformance is evaluated. It is not vendor-specific validation. |

`ComponentStatus` and `OverallStatus` use the semantics in
[result-status-contract.md](../specification/result-status-contract.md). In
particular, `NotEvaluated` and `Absent` are display states, not package result
statuses.

## Value contracts

### Finding

Every exported finding has the following fields. The Core creates them; renderers
only display them.

| Field | Required | Contract |
| --- | --- | --- |
| `RuleId` | Yes | Stable APV rule identifier from the rule dictionary, such as `APV-MAN-004`. Rule existence will be enforced by a later registry task. |
| `Level` | Yes | A `FindingLevel`. |
| `Component` | Yes | A `FindingComponent` that owns the rule evaluation. |
| `Title` | Yes | Short, stable human-readable summary. |
| `Description` | Yes | What was observed and why it violates or qualifies the rule. |
| `Location` | Yes when a package file is relevant | A `FindingLocation`; its `FilePath` is a normalized path relative to the selected package root. |
| `ActualValue` | No | A safe, bounded representation of the observed value. It must be omitted rather than expose a secret or unbounded raw content. |
| `ExpectedValue` | No | Safe, concise expected shape or value. |
| `Explanation` | Yes | User-facing reason this affects portable conformance. |
| `SuggestedFix` | Yes | Actionable corrective guidance; it never requests command execution. |
| `SpecificationReference` | Yes | The normative source title, stable section identifier, and source URL already recorded by the project. The validator does not fetch the URL. |

`FindingLocation` has `FilePath`, optional positive `Line`, optional positive
`Column`, and optional logical entry identifier. It must never contain an
absolute host path, a path outside the selected package root, raw exception
data, or embedded file contents.

`Finding` is immutable once emitted. It carries no exception object, stack
trace, credential, token, or raw YAML/JSON body. Multiple findings may use the
same rule ID when they identify different locations or entries.

### ComponentResult

`ComponentResult` represents exactly one `ComponentKind` and contains:

| Field | Contract |
| --- | --- |
| `Kind` | One of `Manifest`, `Skills`, or `Mcp`; report order is always Manifest, Skills, MCP. |
| `Status` | A `ComponentStatus` permitted for that component. |
| `EntrySummary` | Optional for Manifest; present for an evaluated Skills or MCP collection. It contains non-negative `DiscoveredCount`, `ValidCount`, and `InvalidCount`, with `DiscoveredCount = ValidCount + InvalidCount`. |
| `FindingIndexes` | Stable references to the report's findings owned by the component or its entries. The exported JSON/text format may materialize the findings instead, but cannot change ownership. |

Manifest permits only `Valid`, `Invalid`, and `NotEvaluated`. Skills and MCP
may also be `Partial` and `Absent`. An evaluated non-empty Skills or MCP
component is `Partial` exactly when at least one discovered entry is valid and
at least one is invalid. An invalid entry is skipped for further entry-level
validation; this does not discard findings already produced for it.

### ValidationReport

`ValidationReport` is a complete, immutable package result containing:

| Field | Contract |
| --- | --- |
| `ReportContractVersion` | Version of this Core report contract, independent from the plugin format version. |
| `Target` | A non-secret display identity for the explicitly selected package. The report does not require an absolute input path. |
| `Applicability` | The `ApplicabilityDisposition` and its supporting format findings. |
| `OverallStatus` | One of `Valid`, `Invalid`, or `NotApplicable`. |
| `Components` | Exactly three `ComponentResult` values: Manifest, Skills, and MCP, in stable order. |
| `Findings` | All package-level, component-level, and individual-entry findings in deterministic order. |
| `Summary` | Derived counts by finding level and component status; callers do not supply it independently. |

`ReportSummary` includes `ErrorCount`, `WarningCount`, `InfoCount`, and the
count of components in each `ComponentStatus`. Its values are derived from
`Findings` and `Components`, so a report cannot claim a clean summary while
retaining an error finding.

## Aggregation and status rules

1. If format detection reaches `RecognizedNonPortable`, the report emits
   `APV-FORMAT-001` at `Info` level and returns `OverallStatus.NotApplicable`.
   No vendor-specific component validation occurs.
2. If a fatal portable-manifest error occurs, the report returns
   `OverallStatus.Invalid`; Manifest is `Invalid`, and Skills/MCP are
   `NotEvaluated`.
3. If the manifest is valid, Skills and MCP are evaluated independently. A
   component-level failure affects only its component; it does not suppress the
   other component.
4. For an applicable portable package, one or more `Error` findings make the
   overall status `Invalid`. With no errors, it is `Valid`.
5. `Partial` records mixed entry results for Skills or MCP only. It never
   propagates as an overall status and does not make the package valid if it
   contains an error finding.
6. `Absent` means an optional component was not declared. It has no findings
   and is not treated as a validation failure.

The failure boundaries therefore remain visible in one report:

| Boundary | Model representation |
| --- | --- |
| Fatal manifest | `Manifest = Invalid`, optional components `NotEvaluated`, overall `Invalid` |
| Skills or MCP collection failure | Failing component `Invalid`; sibling component still has its own result |
| One Skill or MCP entry failure | Parent component `Partial` when valid entries remain; entry-owned finding uses `Skill` or `McpServer` |
| Recognized non-portable package | `OverallStatus = NotApplicable` and no portable component result is claimed as conformance evidence |

## Determinism and safety invariants

- Findings are ordered deterministically by component order, then rule ID,
  package-relative file path, line, column, and logical entry identifier.
- A finding always has one owning component; a report-level condition uses
  `FindingComponent.Package`.
- Location and value fields are sanitized before a finding is added. The model
  cannot be used to expose content outside the selected root.
- The report contains observations and remediation text only. It holds no
  executable instruction, process result, network result, or mutation result.
- The model does not imply that files were executed, dependencies installed,
  MCP servers connected, or remote specifications fetched.

## Logical example

This is a structural example only; it does not prescribe JSON serialization.

```text
ValidationReport
  OverallStatus: Invalid
  Components:
    Manifest: Valid
    Skills: Partial (discovered: 2, valid: 1, invalid: 1)
    Mcp: Absent
  Findings:
    APV-SKL-004 | Error | Skill | skills/release/SKILL.md
      Explanation: the entry is missing a required field
      SuggestedFix: add the required field and keep the path inside the package
      SpecificationReference: Agent Skills Specification, frontmatter section
  Summary: errors=1, warnings=0, info=0
```

## Deferred work

- APV-2.3 will define the rule registry and enforce references to the Rule ID
  dictionary and specification records.
- APV-3 will implement bounded package reading, containment checks, and source
  locations without executing package content.
- The Core, CLI, test projects, target framework, package choices, serializers,
  and actual C# types remain uncreated until a later implementation task.
- APV-7 will define machine-readable and human-readable rendering without
  changing this Core contract.
