---
task: APV-1.4
status: accepted
verified: 2026-08-23
---

# APV V0 Scope and Future Roadmap

## README-ready project summary

Agent Plugin Validator (APV) is an open-source developer tool for deterministic conformance validation of **portable Agent Plugins Specification 1.0.0** packages.

The V0 deliverable is a local .NET/C# validation core and CLI. It reads a plugin directory as untrusted static data, explains conformance findings with stable rule IDs and specification references, and never activates plugin-provided content.

APV V0 validates only the portable format. A recognized Codex-, Claude-, Copilot-, or other vendor-only package is reported as `NOT_APPLICABLE`; it is not labeled invalid merely because it is not a portable Agent Plugins 1.0.0 package.

## V0 in scope

| Area | V0 commitment |
| --- | --- |
| Input | A local plugin directory supplied explicitly to the CLI. |
| Portable format | Agent Plugins Specification 1.0.0 only. |
| Manifest | Root `plugin.json`, canonical schema/version, required fields, name constraints, metadata, unknown-field behavior, and extensions behavior. |
| Skills | Fixed `skills/<skill-name>/SKILL.md` discovery plus Agent Skills frontmatter/name/description validation. |
| MCP | Optional root `mcp.json`, top-level and individual server configuration, transport-specific structural rules, version consistency, path/placeholder rules, and deterministic security-relevant configuration rules. |
| Safety | Filesystem-resolved root containment; bounded read-only inspection; no package execution or network runtime activity. |
| Output | Explainable findings with `ERROR` / `WARNING` / `INFO`, rule IDs, component status, suggested fixes, and specification references. |
| Delivery | Human-readable CLI result, useful exit codes, fixtures, automated tests, GitHub Actions build/test, and README. |

The V0 exit gate is met only when the Definition of Done in the Jira backlog is satisfied: local validation works, findings are explainable, specifications’ fatal/non-fatal boundaries are preserved, fixtures and tests exist, CLI exit codes work, and CI/README are complete.

## V0 hard exclusions

The following are intentionally excluded from the V0 backlog. They must not be added as “small extras,” placeholder services, dormant infrastructure, or hidden runtime behavior.

| Exclusion | Why it stays out of V0 |
| --- | --- |
| Web frontend, hosted API, public repository reader | The core validator and CLI must prove product value before host/UI work. |
| Authentication, accounts, teams, database, scan history, billing, payments | These are SaaS concerns without evidence of developer demand. |
| Private GitHub repositories, GitHub App, marketplace, scheduled scans | They require identity, authorization, persistence, and operational scope beyond V0. |
| ZIP uploads, archive extraction, arbitrary remote URL ingestion | They widen the untrusted-input and resource-control surface before the core is proven. |
| Plugin execution, MCP server launch/connection, dynamic behavior testing | APV is static conformance validation, not a plugin runtime or sandbox. |
| Dependency installation, builds, package-manager invocation | They violate the never-execute security contract. |
| Malware detection, prompt-injection scanning, source-code vulnerability analysis, penetration testing | These are different product categories and require different threat models/evidence. |
| LLM analysis | V0 must be deterministic, explainable, and inexpensive to run. |
| Auto-fix pull requests or code modification | Validation must be trusted before automated changes are proposed. |
| Codex/Claude/Copilot-specific conformance validation | Vendor adapters must remain separate from portable conformance results. |
| Compatibility score, security score, or certification claim | Binary conformance plus component detail is more explainable; APV is not a security certification service. |

## ADR-001 — V0 is portable conformance core plus CLI

**Status:** Accepted

### Context

The portable Agent Plugins 1.0.0 format needs an explainable validator. Building hosted product features first would mix the unproven validation logic with UI, identity, persistence, and external repository risks. Treating vendor package formats as portable failures would produce misleading results.

### Decision

APV V0 will implement only a UI- and transport-independent .NET/C# validation core plus a local CLI for Agent Plugins 1.0.0. The engine will preserve the specification's fatal-manifest, component, and individual-entry boundaries. It will inspect static package content only and will not execute or connect to package-provided content.

### Consequences

- The first release can be validated with fixtures and published as an open-source developer tool.
- The API, web, SaaS, vendor-adapter, and dynamic-security opportunities remain viable, but cannot enter V0 without a new explicit decision.
- Future hosts must reuse the core without weakening its no-execution, path-containment, or result-status contracts.

## Roadmap after V0

| Phase | Outcome | Explicit entry gate | Still excluded in that phase |
| --- | --- | --- | --- |
| **V0** | Local validation core + CLI | Current Jira MVP Definition of Done | Hosted UI/API, SaaS, vendor adapters, runtime execution. |
| **V1** | Public GitHub repository URL → isolated repository reader → validation core → web report | V0 fixture suite, CLI behavior, and rule semantics are stable against real public examples | Authentication, database, private repositories, billing. |
| **V1.1** | Machine-readable JSON report | Stable report schema and CLI result semantics | PR mutation and private-repo access. |
| **V1.2** | GitHub Action, PR checks, and annotations | JSON report contract plus CI integration design | Auto-fix PRs and broad marketplace integration. |
| **V1.3** | Suggested fixes and richer diagnostics | Low-risk deterministic remediation catalog reviewed against fixtures | Automatic code/config modification. |
| **V2** | Separate vendor compatibility adapters | Portable conformance validator is mature and vendor rules can be labeled independently | Mixing vendor status with portable conformance. |
| **V3** | SaaS capabilities: login, history, private repositories, GitHub App, teams, scheduling, billing | Demonstrated developer demand, privacy/security design, and operational ownership | Unreviewed automation or a security-certification claim. |

## Scope-change rule

An item in the exclusions table requires all of the following before it can enter delivery work:

1. An explicit user/product decision.
2. A new Jira task with acceptance criteria and dependencies.
3. An updated threat model and safety contract when it changes trust, execution, networking, identity, or data retention.
4. A vault decision and updated roadmap.
5. Tests and delivery evidence proportionate to the new risk.

No future-phase feature is implicitly authorized by this roadmap.

## APV-1 acceptance trace

| APV-1 acceptance criterion | Source-controlled evidence |
| --- | --- |
| Only portable Agent Plugins 1.0.0 is validated | This document; [rule matrix](../specification/rule-matrix.md). |
| No plugin code/scripts/MCP/package managers run | [Never-execute contract](../security/never-execute-contract.md). |
| `VALID`, `INVALID`, `PARTIAL`, `NOT_APPLICABLE` are defined | [Result-status contract](../specification/result-status-contract.md). |
| Fatal, component, and entry boundaries are defined | [Rule matrix](../specification/rule-matrix.md); [result-status contract](../specification/result-status-contract.md). |
| Web/SaaS/auth/ZIP/vendor validation exclusions are explicit | This document. |

## README handoff note

The public README should reuse the **README-ready project summary**, **V0 in scope**, **V0 hard exclusions**, and selected examples from the linked contracts. APV-9 owns the final README after the executable CLI, fixtures, and CI evidence exist.
