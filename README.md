# Agent Plugin Validator

[![Build and Test](https://github.com/erayyilmmaz/agent-plugin-validator/actions/workflows/build-test.yml/badge.svg)](https://github.com/erayyilmmaz/agent-plugin-validator/actions/workflows/build-test.yml)

Agent Plugin Validator (APV) is a deterministic local CLI for checking whether
a directory conforms to the portable **Agent Plugins Specification 1.0.0**.
It reports explainable, spec-referenced findings without activating the plugin.

> **Safety guarantee: No plugin-provided code is executed.** APV does not run
> scripts, commands, MCP servers, package managers, builds, or network
> connections while validating a package.

## What APV validates

- Root `plugin.json` using the canonical Agent Plugins 1.0.0 schema.
- Fixed-location `skills/<skill-name>/SKILL.md` discovery and required
  frontmatter constraints.
- Root `mcp.json`, canonical schema/version consistency, and closed `stdio`,
  `streamable-http`, and `sse` transport configurations.
- Plugin-root containment, known placeholder forms, reserved environment names,
  remote URL/header constraints, and finite deterministic secret evidence.

APV reports one overall status (`VALID`, `INVALID`, or `NOT_APPLICABLE`) plus
Manifest, Skills, and MCP component results. It deliberately has no
compatibility percentage or weighted score.

## Quick start

APV requires the SDK version pinned in [`global.json`](global.json).

```sh
dotnet restore AgentPluginValidator.sln
dotnet build AgentPluginValidator.sln --configuration Release --no-restore
dotnet run --project src/AgentPluginValidator.Cli -- \
  validate <plugin-directory>
```

For CI-friendly one-line output:

```sh
dotnet run --project src/AgentPluginValidator.Cli -- \
  validate <plugin-directory> --ci
```

`--quiet` suppresses normal output while preserving the exit status.

| Exit code | Meaning |
| --- | --- |
| `0` | Valid portable package |
| `1` | Invalid portable package |
| `2` | Input or usage error |
| `3` | Recognized non-portable/vendor-only package |

## Example output

Valid portable package:

```text
Agent Plugin Validator
Target: full-valid
Overall: VALID
Manifest: VALID
Skills: VALID (entries: 1 valid, 0 invalid, 1 discovered)
MCP: VALID (entries: 3 valid, 0 invalid, 3 discovered)
Findings: Errors: 0, Warnings: 0, Info: 0
```

Invalid MCP header configuration:

```text
Overall: INVALID
Manifest: VALID
Skills: ABSENT (entries: 0 valid, 0 invalid, 0 discovered)
MCP: INVALID (entries: 0 valid, 1 invalid, 1 discovered)
Findings: Errors: 1, Warnings: 0, Info: 0

ERROR [APV-MCP-022] McpServer (mcp.json#mcpServers.remote)
  Remote headers contain a deterministically identifiable credential or secret.
```

## Portable conformance, not vendor validation

APV validates only the portable Agent Plugins 1.0.0 format. If it recognizes a
vendor-only package, such as a Codex-specific package without a portable root
manifest, it returns `NOT_APPLICABLE` with `APV-FORMAT-001`. That is not a
portable conformance error, and APV does **not** parse, execute, or validate the
vendor’s configuration format.

## Architecture

```text
explicit local directory
  -> SafePackageReader (bounded, contained, read-only)
  -> PluginValidator (manifest -> Skills and MCP orchestration)
  -> ValidationReport (statuses, findings, derived counts)
  -> CLI renderer (human / quiet / CI output, exit code)
```

The reusable Core owns validation rules, aggregation, and finding metadata. The
CLI only parses `validate`, renders a Core report, and maps its result to an
exit code. See [solution boundaries](docs/architecture/solution-boundaries.md)
and [report orchestration](docs/architecture/report-orchestration-and-cli.md).

## Security boundary and limitations

APV treats plugin directories as untrusted static data. It does not execute or
connect to plugin-provided content, does not install dependencies, and does not
read paths outside the filesystem-resolved plugin root. See the
[never-execute contract](docs/security/never-execute-contract.md).

APV is **not** a security certification, malware scanner, sandbox, dynamic
behavior test, vulnerability assessment, runtime MCP compatibility test, or
secret-free guarantee. Its deterministic secret findings cover only finite,
documented configuration evidence.

The MVP does not include a web UI, SaaS/API, authentication, database, ZIP
upload, remote repository ingestion, vendor adapters, automatic fixes, or a
compatibility score.

## Rule catalog

| Area | Rule families | Details |
| --- | --- | --- |
| Package and manifest | `APV-PACKAGE-*`, `APV-PATH-*`, `APV-MANIFEST-*` | Root loading, canonical schema, metadata, and non-fatal manifest exceptions |
| Skills | `APV-SKILL-*` | Fixed discovery, frontmatter, name, and description |
| MCP | `APV-MCP-*`, `APV-CROSS-*`, `APV-COMPONENT-*` | Top-level config, transports, paths, headers, env, and version consistency |
| Applicability | `APV-FORMAT-001` | Recognized vendor-only package is not applicable |

The [rule matrix](docs/specification/rule-matrix.md) and
[rule ID dictionary](docs/specification/rule-id-dictionary.md) are the complete
stable diagnostic catalog. Regression coverage and the local quality gate are
documented in [the fixture regression suite](docs/quality/fixture-regression-suite.md).

## Development verification

```sh
dotnet test AgentPluginValidator.sln --configuration Release --no-restore --nologo
```

The GitHub Actions workflow runs restore, Release build, and the full test suite
from a clean checkout on pushes to `main` and pull requests.
