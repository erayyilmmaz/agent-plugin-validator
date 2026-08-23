---
task: APV-8
status: implemented
verified: 2026-08-23
---

# APV Fixture-driven Regression Suite

`tests/fixtures/apv8` is the package-level regression inventory. Each fixture
is inert UTF-8 static input and is validated through `PluginValidator`; no
fixture command, script, MCP server, package manager, URL, or plugin code is
executed.

## Inventory and expected boundaries

| Fixture | Overall | Boundary evidence |
| --- | --- | --- |
| `minimal-valid` | Valid | Minimal canonical manifest; optional components absent |
| `full-valid` | Valid | Manifest, Skill, stdio, streamable HTTP, and SSE all valid |
| `invalid-manifest` | Invalid | `APV-MANIFEST-004`; Skills/MCP not evaluated |
| `invalid-skill` | Invalid | `APV-SKILL-004`; valid MCP remains visible |
| `invalid-mcp` | Invalid | `APV-MCP-001`; valid Skill remains visible |
| `version-mismatch` | Invalid | `APV-CROSS-001`; MCP only disabled |
| `path-traversal` | Invalid | `APV-MCP-011`; offending server only invalid |
| `secret-header` | Invalid | `APV-MCP-022`; finite deterministic secret evidence |

Existing APV-3 through APV-6 fixtures remain the narrow rule-level suite. In
particular, `apv6/mixed` regression-asserts the remaining individual MCP rule
IDs (`003`, `010`–`013`, `020`–`023`) while `full-valid` provides their valid
transport/configuration counterpart.

## Determinism and minimum quality gate

The regression test serializes the complete report signature: overall status,
ordered components and entry counts, ordered finding identity/location/source,
and severity totals. It validates repeated fresh-reader runs over the same
fixture and requires an identical signature.

Run the required local quality gate:

```sh
DOTNET_CLI_HOME=/private/tmp/apv-dotnet-cli \
NUGET_PACKAGES=/private/tmp/apv-nuget \
/Users/eray-refgen/.dotnet/dotnet test AgentPluginValidator.sln \
  --configuration Release --no-restore --nologo
```

The gate passes only when all Core and CLI tests pass, no fixture has any Unix
execute bit, and a source scan finds no process, HTTP client, socket, dynamic
load, or package-manager API in the validator projects. APV intentionally does
not impose a percentage/coverage score as a conformance metric.
