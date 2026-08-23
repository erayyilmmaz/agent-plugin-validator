---
task: APV-2.1
status: approved-boundary
verified: 2026-08-23
---

# APV Core, CLI, and Test Solution Boundaries

## Decision

APV V0 will use a .NET/C# solution with a reusable validation core, a thin local CLI host, and separate test projects. This document defines their boundaries before any `.sln`, `.csproj`, source directory, parser package, or fixture tree is scaffolded.

The current checkout contains documentation only. The local development host has .NET SDK `10.0.400`, but this document does **not** commit a target framework, package version, test framework, CLI library, JSON-schema library, YAML library, or final filesystem layout. Those decisions require APV-2.2 through APV-2.4 evidence.

## Intended solution shape

The following is the approved **target boundary**, not a statement that these paths exist yet:

```text
AgentPluginValidator.sln
├── src/
│   ├── AgentPluginValidator.Core/          # reusable class library
│   └── AgentPluginValidator.Cli/           # local console host
├── tests/
│   ├── AgentPluginValidator.Core.Tests/    # deterministic core/rule tests
│   ├── AgentPluginValidator.Cli.Tests/     # CLI/report/exit-code behavior tests
│   └── fixtures/                           # inert plugin package test data
└── docs/
```

Project names and root folders are reserved to make future work unambiguous. They are created only when a later implementation task explicitly scaffolds them.

## Dependency direction

```text
AgentPluginValidator.Cli ───────▶ AgentPluginValidator.Core
          │                                   ▲
          │                                   │
          └──────── CLI tests ────────────────┘

AgentPluginValidator.Core.Tests ────────────▶ AgentPluginValidator.Core
tests/fixtures ───── inert input data ──────▶ test projects only
```

Dependencies must point toward the core. The Core project must never reference the CLI, console rendering, command-line parsing, web/API hosts, database, runtime MCP clients, or vendor adapters.

## Core boundary

`AgentPluginValidator.Core` owns deterministic validation behavior and reusable contracts.

### Core responsibilities

- Accept an explicit local validation request through a transport-neutral public entry point.
- Coordinate format detection, safe package inspection, manifest validation, skill discovery/validation, MCP validation, cross-component checks, finding collection, and report aggregation.
- Define the finding, rule, component-result, and report contracts designed in APV-2.2 and APV-2.3.
- Preserve the rule-specific failure boundaries and result-status semantics defined in the existing specification contracts.
- Expose data structures that a future CLI, API, editor integration, or GitHub Action can render without duplicating validation logic.

### Core prohibitions

The Core project must not:

- write console output or parse CLI arguments;
- launch processes, invoke shells/package managers, load plugin code, make network requests, or connect to MCP servers;
- contain web, authentication, database, billing, GitHub client, ZIP-upload, or vendor-adapter behavior;
- resolve a plugin-provided path outside the allowed plugin root;
- hide rule/severity/failure-boundary decisions in a host-specific presentation layer.

### Filesystem seam

APV needs controlled local filesystem inspection, but a public report must not depend on a concrete host. The Core owns the **safe package-reader contract** and its validation semantics; APV-3 decides the default filesystem implementation and limits. Tests must be able to replace that seam with deterministic controlled input.

This is not a generic infrastructure layer. V0 does not create an additional repository, service, plugin runtime, or dependency-injection framework merely to read local package files.

## CLI boundary

`AgentPluginValidator.Cli` is the local user-facing composition and presentation host.

### CLI responsibilities

- Parse the `validate <plugin-directory>` command and future documented flags.
- Validate invocation syntax and map invocation/input failures to exit code `2`.
- Construct a Core validation request using the explicit local directory argument.
- Render the human-readable report and choose stdout/stderr formatting.
- Map the Core overall result to the documented exit codes: `0` valid, `1` invalid, `3` not applicable.

### CLI prohibitions

The CLI must not:

- reimplement manifest, skill, MCP, path, finding, result-status, or rule-ID logic;
- execute package content while preparing or rendering a report;
- add a web server, authentication, database, history, telemetry, remote repository reader, or dependency installer;
- make a component look valid/invalid by formatting or exit-code policy that contradicts the Core report.

The CLI may depend on Core. Core may never depend on CLI.

## Test boundary

### Core tests

`AgentPluginValidator.Core.Tests` tests deterministic rule behavior, failure boundaries, aggregation, path policy, and finding/report contracts. Tests use fixtures and controlled filesystem seams; they do not execute fixture scripts, MCP configuration, commands, or network endpoints.

### CLI tests

`AgentPluginValidator.Cli.Tests` tests command parsing, input/usage error handling, rendering, quiet/CI behavior, and exit-code mapping. It verifies the CLI consumes Core results rather than reproducing rules.

### Fixtures

`tests/fixtures/` stores inert package trees and small text/config files only. Fixture content may intentionally contain hostile command strings, URLs, traversal paths, malformed JSON/YAML, and credential-like values, but no test may activate them. Fixture naming and helper conventions are specified in APV-2.4.

## Deferred decisions

| Decision | Owner task | Why deferred |
| --- | --- | --- |
| Finding/report/component-status types and public namespace | APV-2.2 | These contracts must first reflect APV-1.1 and APV-1.3 exactly. |
| Rule registry and specification-reference representation | APV-2.3 | It should follow the finalized finding contract. |
| Fixture taxonomy, helpers, temporary directories, and test-data safety conventions | APV-2.4 | It must align with the package-reader boundary. |
| Target framework, solution scaffold, parser/schema/CLI/test packages, and package versions | First implementation task after APV-2 | Choose maintained packages with current evidence, not a planning-time assumption. |
| Default safe filesystem reader, limits, canonicalization, and symlink behavior | APV-3 | These require implementation-specific cross-platform checks. |

## Acceptance trace

| APV-2 acceptance criterion | APV-2.1 evidence |
| --- | --- |
| Validation core is independent of CLI | Dependency direction, Core/CLI responsibilities and prohibitions. |
| Findings support required fields | Deferred explicitly to APV-2.2; no host may own findings. |
| Manifest, Skill, MCP, and Package results aggregate under one report | Deferred explicitly to APV-2.2; Core owns aggregation. |
| .NET Core + CLI boundaries documented without empty-checkout assumptions | This document reserves target boundaries without scaffolding projects or selecting packages. |

## Change control

Adding a Core → CLI dependency, moving validation rules into the CLI, introducing a separate runtime/infrastructure service, or adding any host feature excluded by V0 requires an explicit architecture decision, Jira task, vault update, and verification plan.
