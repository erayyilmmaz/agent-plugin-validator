---
task: APV-1.1
status: approved-baseline
specification: Agent Plugins 1.0.0
verified: 2026-08-23
---

# APV-1.1 Normative Rule Matrix

## Purpose and authority

This matrix translates only the normative package-validation requirements needed by the APV V0 backlog into stable APV diagnostics. The [Agent Plugins Specification 1.0.0](https://github.com/agentplugins/agent-plugins-spec/blob/main/spec/1.0.0.md) is authoritative over this document and its JSON schemas. [Agent Skills](https://agentskills.io/specification) is authoritative for the `SKILL.md` format.

APV validates a local directory as untrusted data. It does not execute plugin code, scripts, MCP servers, commands, builds, package managers, or network connections. Runtime-only requirements are captured for traceability but are not exercised by APV V0.

## Boundary legend

| Boundary | APV behavior |
| --- | --- |
| **Plugin** | Stop component discovery. The package is invalid. |
| **Component** | Mark only the named component invalid/disabled and continue independently valid components. |
| **Entry** | Skip only the named skill or MCP server entry and continue sibling entries/components. |
| **Continue** | Emit the finding and retain loading of otherwise valid content. |
| **Deny access** | Do not read the offending resolved path. Apply the more specific boundary listed by the specification. |

## Package and path safety

| Rule ID | Normative requirement | Level | Boundary | Source | V0 implementation note |
| --- | --- | --- | --- | --- | --- |
| `APV-PACKAGE-001` | A plugin is a directory with `plugin.json` at its root; no other file replaces or overrides that manifest. | ERROR | Plugin | Agent Plugins §4.1.1–2, §5.1 | Validate root manifest before any component discovery. |
| `APV-PATH-001` | A discovered/read package path must resolve within the filesystem-resolved plugin root; escapes through symlinks, junctions, reparse points, or equivalents are rejected. | ERROR | Context-specific | Agent Plugins §4.1.3, §4.1 failure list | The concrete boundary is: manifest → Plugin; fixed component → Component; skill → Entry; MCP command/cwd → Entry; other path → Deny access. |
| `APV-PATH-002` | A field defined as a plugin-relative path begins with `./`, resolves from plugin root, and remains contained. Do not treat opaque arguments or env values as paths. | ERROR | Context-specific | Agent Plugins §4.1.4–5 | Apply only to fields that the specification defines as paths; do not reinterpret arbitrary strings. |

## Manifest (`plugin.json`)

| Rule ID | Normative requirement | Level | Boundary | Source | V0 implementation note |
| --- | --- | --- | --- | --- | --- |
| `APV-MANIFEST-001` | Manifest is valid JSON with a top-level object. | ERROR | Plugin | Agent Plugins §5.2 | Fatal unless a named non-fatal exception applies. |
| `APV-MANIFEST-002` | `$schema` is required and is the recognized canonical `https://agent-plugins.org/schemas/1.0.0/plugin.schema.json`; APV must not fetch schemas while loading. Unsupported versions are rejected. | ERROR | Plugin | Agent Plugins §5.2–§5.3 | APV supports only 1.0.0 in V0. |
| `APV-MANIFEST-003` | `name` is required, string, and non-empty. | ERROR | Plugin | Agent Plugins §5.3 | Missing, wrong-type, or empty required fields are fatal. |
| `APV-MANIFEST-004` | `name` is 1–64 characters; only lowercase `a-z`, `0-9`, `-`, `.`; starts/ends alphanumeric; has no `--` or `..`. | ERROR | Plugin | Agent Plugins §5.5 | Periods are valid in plugin names. |
| `APV-MANIFEST-005` | Permitted metadata has its required JSON type; `author` is a closed object with only optional string `name`, `email`, `url` fields. | ERROR | Plugin | Agent Plugins §5.2, §5.4 | Do not add extra semantic checks for SemVer, URLs, email, or SPDX identifiers. |
| `APV-MANIFEST-006` | An unknown top-level field is reported and ignored; APV must assign it no semantics. | WARNING | Continue | Agent Plugins §5.2 | It is a schema nonconformance but is explicitly non-fatal. |
| `APV-MANIFEST-007` | A non-object `extensions` field is reported and ignored. Unimplemented extension namespaces are ignored without content validation. | WARNING | Continue | Agent Plugins §5.2, §8.1 | Do not treat unknown extension contents as portable validation input. |

## Component discovery

| Rule ID | Normative requirement | Level | Boundary | Source | V0 implementation note |
| --- | --- | --- | --- | --- | --- |
| `APV-COMPONENT-001` | Supported skills are discovered only from fixed `skills/`; `plugin.json` cannot override core locations. A present `skills/` that is not a directory invalidates Skills only. | ERROR | Component | Agent Plugins §6.1–§6.2 | Missing `skills/` is not a finding. |
| `APV-COMPONENT-002` | Supported MCP configuration is only root `mcp.json`; a present `mcp.json` that is not a regular file invalidates MCP only. | ERROR | Component | Agent Plugins §6.1–§6.2, §7.2.1 | Missing `mcp.json` is not a finding. |

## Agent Skills

| Rule ID | Normative requirement | Level | Boundary | Source | V0 implementation note |
| --- | --- | --- | --- | --- | --- |
| `APV-SKILL-001` | Only each immediate child of `skills/` with an exactly named, contained, regular `SKILL.md` is a discovered skill. A `SKILL.md` resolving outside root is skipped. | ERROR | Entry | Agent Plugins §4.1, §7.1 | Do not recurse deeper to discover extra skills. A child with no `SKILL.md` is not a discovered skill and produces no finding. |
| `APV-SKILL-002` | `SKILL.md` contains YAML frontmatter followed by Markdown. | ERROR | Entry | Agent Skills: SKILL.md format | A missing or unparseable frontmatter prevents validation of that skill. |
| `APV-SKILL-003` | Declared skill `name` matches its parent directory name. | ERROR | Entry | Agent Skills: name field | Keep this dedicated ID for the most actionable mismatch diagnosis. |
| `APV-SKILL-004` | `name` is required, 1–64 characters, lowercase alphanumeric/hyphen only, does not start/end with `-`, and has no `--`. | ERROR | Entry | Agent Skills: frontmatter, name field | Validate this independently of parent-directory matching. |
| `APV-SKILL-005` | `description` is required, non-empty, and 1–1024 characters. | ERROR | Entry | Agent Skills: frontmatter, description field | Quality recommendations about wording are not V0 errors. |

## MCP top-level configuration and cross-file consistency

| Rule ID | Normative requirement | Level | Boundary | Source | V0 implementation note |
| --- | --- | --- | --- | --- | --- |
| `APV-MCP-001` | `mcp.json` is a JSON object with only required `$schema` and `mcpServers`; `mcpServers` is an object and may be empty. | ERROR | Component | Agent Plugins §7.2.1–§7.2.2 | Invalid JSON or invalid top-level MCP shape disables MCP, not Skills. |
| `APV-MCP-002` | MCP `$schema` is required, recognized, canonical, and must be selected locally without fetching schemas. | ERROR | Component | Agent Plugins §7.2.1–§7.2.2 | APV supports only `https://agent-plugins.org/schemas/1.0.0/mcp.schema.json` in V0. |
| `APV-CROSS-001` | Present MCP schema version matches manifest schema version. | ERROR | Component | Agent Plugins §7.2.2, §10.1 | Mismatch invalidates MCP only; other component types remain independently valid. |
| `APV-MCP-003` | Every server has `type` and matches exactly one closed variant. Unknown fields, unknown types, or fields from another variant invalidate that entry. | ERROR | Entry | Agent Plugins §7.2.1 | Validate entries independently to preserve the narrow boundary. |

## MCP server entries

| Rule ID | Normative requirement | Level | Boundary | Source | V0 implementation note |
| --- | --- | --- | --- | --- | --- |
| `APV-MCP-010` | `stdio.command` is one executable token: a bare name or `./` plugin-relative path. It is not a shell command and receives no placeholder expansion. | ERROR | Entry | Agent Plugins §7.2.1 | Validate syntax and containment only; never resolve/run an executable. |
| `APV-MCP-011` | `stdio.cwd` is `./…`, `${PLUGIN_ROOT}` / `${PLUGIN_ROOT}/…`, or `${PLUGIN_DATA}` / `${PLUGIN_DATA}/…`; it must remain in its applicable resolved root. | ERROR | Entry | Agent Plugins §7.2.1 | Validate plugin-root paths directly. Validate `${PLUGIN_DATA}` form only; APV has no client-managed data directory to resolve. |
| `APV-MCP-012` | `stdio.args`, `env`, and `cwd` support only exact `${PLUGIN_ROOT}` / `${PLUGIN_DATA}` expansion. No expansion applies to command, env keys, or fixed locations. | ERROR | Entry | Agent Plugins §7.2.1, §9.2 | Validate eligible field/type/placeholder use without expanding or launching anything. |
| `APV-MCP-013` | `stdio.env` does not configure reserved `PLUGIN_ROOT` or `PLUGIN_DATA`. | ERROR | Entry | Agent Plugins §9.2 | APV detects names only; it does not construct a subprocess environment. |
| `APV-MCP-020` | Remote `url` is absolute HTTP(S), has no user info or fragment, and uses HTTPS except for exact localhost or loopback hosts. | ERROR | Entry | Agent Plugins §7.2.1 | Do not connect to the URL. |
| `APV-MCP-021` | Remote headers are valid HTTP fields, do not collide case-insensitively, and do not contain placeholder/environment expansion. | ERROR | Entry | Agent Plugins §7.2.1 | Do not send headers or follow redirects. |
| `APV-MCP-022` | Portable remote headers do not embed credentials or secrets where this can be deterministically established. | ERROR | Entry | Agent Plugins §7.2.1 | Exact detection evidence policy is deferred to APV-6; no broad heuristic scanning. |
| `APV-MCP-023` | Portable `stdio.env` does not embed credentials or secrets where this can be deterministically established. | ERROR | Entry | Agent Plugins §9.2 | Exact detection evidence policy is deferred to APV-6; no broad heuristic scanning. |

## Explicitly not APV V0 validation rules

| Topic | Why it is excluded |
| --- | --- |
| Starting a process, connecting/authenticating, handshake success, redirects, or runtime failure | Those are client runtime concerns. Agent Plugins requires resilient runtime behavior, but APV V0 never executes or connects. |
| Creating/persisting a client-managed `PLUGIN_DATA` directory | Only a client that launches a subprocess has that obligation; APV does not launch one. |
| SemVer validity, URL reachability, email validity, SPDX validity, or subjective skill-description quality | The specifications do not make these fatal package-validation requirements. |
| Codex, Claude, Copilot, or legacy OpenPlugin package conformance | APV V0 validates only portable Agent Plugins 1.0.0. A recognized non-portable format receives the product status `APV-FORMAT-001` / `NOT_APPLICABLE`, not a portable-conformance error. Vendor semantics are not validated. |
| Web UI, SaaS, authentication, database, ZIP uploads, dynamic security scans, malware scans, or LLM analysis | Explicit MVP exclusions; see APV-1.4. |

## Review checklist

- Every `ERROR` maps to a specific required package rule and a narrow failure boundary.
- Every `WARNING` reflects an explicit non-fatal exception in the specification.
- No row requires APV to execute, install, build, connect to, or trust package-provided content.
- Future implementation must preserve both the Rule ID and the stated boundary when adding fixtures and tests.
