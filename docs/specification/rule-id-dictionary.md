---
task: APV-1.1
status: approved-baseline
specification: Agent Plugins 1.0.0
verified: 2026-08-23
---

# APV Rule ID Dictionary

This document reserves the stable diagnostic namespaces used by the Agent Plugin Validator (APV) V0. It is the companion to [the normative rule matrix](rule-matrix.md).

## Stability contract

- A published Rule ID is never renamed or reused for a different condition.
- New diagnostics append a new ID; superseded diagnostics are marked deprecated and retain their historical meaning.
- A finding may include more than one location, but it has exactly one primary Rule ID.
- `ERROR` means a deterministically established specification violation. `WARNING` is reserved for specification-defined non-fatal conformance exceptions. `INFO` is reserved for product status or guidance, not a conformance failure.
- Rule IDs do not claim that APV executed, connected to, built, or installed any package-provided code.

## Namespaces

| Namespace | Purpose | Allocated IDs | Planned implementation task |
| --- | --- | --- | --- |
| `APV-PACKAGE` | Plugin root and required root manifest | `001–099` | APV-3, APV-4 |
| `APV-PATH` | Filesystem-resolved containment and portable path syntax | `001–099` | APV-3, APV-6 |
| `APV-MANIFEST` | `plugin.json` parsing and semantics | `001–099` | APV-4 |
| `APV-COMPONENT` | Fixed component location discovery | `001–099` | APV-5, APV-6 |
| `APV-SKILL` | Agent Skills discovery and `SKILL.md` validation | `001–099` | APV-5 |
| `APV-MCP` | `mcp.json` and individual MCP server configuration | `001–099` | APV-6 |
| `APV-CROSS` | Cross-component version consistency | `001–099` | APV-6 |
| `APV-FORMAT` | Product-level format applicability messages | `001–099` | APV-4 |

## Reserved identifiers

| Rule ID | Stable meaning | Default level | Boundary |
| --- | --- | --- | --- |
| `APV-PACKAGE-001` | Root `plugin.json` is absent, unreadable, or not a root manifest | ERROR | Plugin |
| `APV-PATH-001` | A package path resolves outside the filesystem-resolved plugin root | ERROR | Context-specific; see matrix |
| `APV-PATH-002` | A specification-defined plugin-relative path does not begin with `./` | ERROR | Context-specific; see matrix |
| `APV-MANIFEST-001` | `plugin.json` is not valid JSON or is not a top-level object | ERROR | Plugin |
| `APV-MANIFEST-002` | Manifest `$schema` is absent, non-canonical, unsupported, or otherwise not recognized by APV | ERROR | Plugin |
| `APV-MANIFEST-003` | Required manifest `name` is absent, non-string, or empty | ERROR | Plugin |
| `APV-MANIFEST-004` | Manifest `name` violates Agent Plugins name constraints | ERROR | Plugin |
| `APV-MANIFEST-005` | A permitted manifest metadata field has an invalid type or an invalid `author` shape | ERROR | Plugin |
| `APV-MANIFEST-006` | Manifest has an unknown top-level field | WARNING | Continue plugin loading |
| `APV-MANIFEST-007` | Manifest `extensions` is not an object | WARNING | Ignore `extensions`; continue plugin loading |
| `APV-COMPONENT-001` | Present `skills/` location is not a directory | ERROR | Skills component only |
| `APV-COMPONENT-002` | Present root `mcp.json` is not a regular file | ERROR | MCP component only |
| `APV-SKILL-001` | Discovered `SKILL.md` fails the package-containment or regular-file requirement | ERROR | Skip that skill |
| `APV-SKILL-002` | `SKILL.md` lacks parseable YAML frontmatter | ERROR | Skip that skill |
| `APV-SKILL-003` | Declared skill name does not match its parent directory | ERROR | Skip that skill |
| `APV-SKILL-004` | Required skill name is missing or violates Agent Skills name constraints | ERROR | Skip that skill |
| `APV-SKILL-005` | Required skill description is missing, empty, non-string, or exceeds the allowed length | ERROR | Skip that skill |
| `APV-MCP-001` | `mcp.json` is not valid JSON, is not an object, or fails required top-level shape | ERROR | Disable MCP component |
| `APV-MCP-002` | MCP `$schema` is absent, non-canonical, unsupported, or otherwise not recognized by APV | ERROR | Disable MCP component |
| `APV-CROSS-001` | `mcp.json` schema version differs from `plugin.json` schema version | ERROR | Disable MCP component |
| `APV-MCP-003` | An MCP server is not one closed, supported server variant | ERROR | Skip that server entry |
| `APV-MCP-010` | `stdio.command` is not one bare executable token or a `./` plugin-relative executable path | ERROR | Skip that server entry |
| `APV-MCP-011` | `stdio.cwd` has an unsupported form or violates its applicable containment rule | ERROR | Skip that server entry |
| `APV-MCP-012` | `stdio` arguments, environment, or placeholder use violates the portable configuration rules | ERROR | Skip that server entry |
| `APV-MCP-013` | `stdio.env` overrides reserved `PLUGIN_ROOT` or `PLUGIN_DATA` | ERROR | Skip that server entry |
| `APV-MCP-020` | Remote server URL is not an allowed absolute HTTP(S) endpoint | ERROR | Skip that server entry |
| `APV-MCP-021` | Remote headers are invalid, collide case-insensitively, or use disallowed expansion | ERROR | Skip that server entry |
| `APV-MCP-022` | A deterministically identifiable credential or secret is embedded in remote headers | ERROR | Skip that server entry |
| `APV-MCP-023` | A deterministically identifiable credential or secret is embedded in `stdio.env` | ERROR | Skip that server entry |
| `APV-FORMAT-001` | A recognized vendor-only format is present but portable Agent Plugins 1.0.0 is not declared | INFO | `NOT_APPLICABLE` result |

## Deferred identifiers

The `APV-MCP-022` and `APV-MCP-023` IDs are reserved by the normative prohibition on embedding secrets. Their exact deterministic evidence policy belongs to APV-6. V0 must not use broad heuristic scanning or claim a package is secret-free.

Runtime client duties—such as starting MCP servers, connecting to endpoints, expanding values at runtime, or creating `PLUGIN_DATA`—are not validation actions in APV V0. They are therefore documented as non-executed constraints in the matrix rather than executable APV rules.
