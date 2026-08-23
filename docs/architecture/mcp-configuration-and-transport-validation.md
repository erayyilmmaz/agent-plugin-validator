---
task: APV-6
status: implemented
verified: 2026-08-23
---

# APV MCP Configuration and Transport Validation

`McpValidator` consumes a valid `ManifestValidationResult` and a
`SafePackageReader`. It reads only root `mcp.json`; it never launches a stdio
command, expands a placeholder, connects to a remote endpoint, sends headers,
or loads a schema.

## Failure boundaries

- Missing `mcp.json` is `Absent` without a finding.
- Invalid JSON, top-level shape, canonical schema, or schema-version mismatch
  makes only MCP `Invalid`; Skills remain independently evaluable.
- Each `mcpServers` member is validated independently. Mixed valid/invalid
  servers produce `Partial`; an invalid server is skipped.
- A manifest that blocks component discovery produces MCP `NotEvaluated` and
  no MCP file read.

## Static checks

The root object is closed to `$schema` and `mcpServers`, and supports only
`https://agent-plugins.org/schemas/1.0.0/mcp.schema.json`. Server variants are
closed: `stdio` (`command`, `args`, `env`, `cwd`) and remote
`streamable-http`/`sse` (`url`, `headers`).

- `command` is one bare executable token or a `./` path whose existing links
  remain contained; APV does not require, read, or execute the final file.
- `cwd` accepts only contained `./` / `${PLUGIN_ROOT}` forms or syntactically
  contained `${PLUGIN_DATA}` forms. `${PLUGIN_DATA}` is never materialized.
- Remote URLs are checked locally for scheme, authority, user-info, fragment,
  loopback HTTP exception, headers, and case-insensitive duplicate names.
- Recognized `${PLUGIN_ROOT}`/`${PLUGIN_DATA}` placeholders are forbidden in
  remote headers and are never expanded by APV.
- `PLUGIN_ROOT` and `PLUGIN_DATA` cannot be stdio `env` keys.

## Deterministic secret policy

APV emits `APV-MCP-022`/`023` only for finite, reviewable evidence: the exact
env keys `API_KEY`, `API_TOKEN`, `ACCESS_TOKEN`, `AUTH_TOKEN`, `CLIENT_SECRET`,
`PASSWORD`, `SECRET`, or `TOKEN`; exact sensitive HTTP header names; or values
beginning `Bearer `, `Basic `, or `sk-`. This is not heuristic scanning and it
does not assert that other configuration is secret-free.

## Verification

Fixtures cover absent, invalid top-level, cross-version mismatch, all supported
transports, closed variants, command/cwd traversal, placeholders, reserved env,
external HTTP, duplicate headers, and deterministic header/env secrets. Release
test verification passed 28/28 tests.
