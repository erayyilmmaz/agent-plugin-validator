---
task: APV-1.2
status: approved-baseline
verified: 2026-08-23
---

# APV Never-Execute Security Contract

## Purpose

Agent Plugin Validator (APV) validates an untrusted local plugin directory as **static data**. It provides deterministic Agent Plugins 1.0.0 conformance diagnostics; it is not a plugin runtime, sandbox, malware scanner, penetration-testing tool, or security certification service.

This contract is a product invariant for APV V0. It applies to the validation core, CLI, tests, fixtures, CI workflow, and any future host that reuses the core.

Sources: [APV normative rule matrix](../specification/rule-matrix.md#explicitly-not-apv-v0-validation-rules), [APV rule ID dictionary](../specification/rule-id-dictionary.md), and the [Agent Plugins 1.0.0 path rules](https://github.com/agentplugins/agent-plugins-spec/blob/main/spec/1.0.0.md#41-general-requirements).

## Non-negotiable invariant

Given any plugin directory, APV **MUST NOT** execute, build, install, connect to, authenticate with, or otherwise activate content supplied by that directory.

The only permitted interaction with plugin-provided content is bounded, read-only filesystem inspection and in-memory parsing required to produce the documented conformance report.

## Allowed operations

APV may perform only the following on a supplied plugin directory:

1. Resolve the explicit plugin-root path and inspect filesystem metadata needed to enforce containment.
2. Enumerate the fixed portable locations defined by the supported specification.
3. Read bounded text content from contained regular files needed for validation: root `plugin.json`, root `mcp.json`, and discovered `skills/<name>/SKILL.md` files.
4. Parse JSON, YAML frontmatter, Markdown metadata, URLs, and strings entirely in memory.
5. Produce findings in memory and write the APV report to its caller-controlled output stream or a caller-selected output location outside the plugin package.

APV does not grant additional permission merely because a configuration string names a command, URL, environment variable, script, binary, package, credential, or external file.

## Prohibited operations

### Process and shell execution

APV MUST NOT:

- invoke a process, shell, interpreter, script, binary, build tool, test runner, or task runner from a plugin package;
- pass plugin-derived values to process-launching APIs, command-line parsers, shell expansion, `eval`, or equivalent dynamic execution facilities;
- load plugin-provided assemblies, native libraries, modules, templates, or code into the APV process;
- treat `mcp.json` `command`, `args`, `env`, or `cwd` as execution instructions.

In particular, the future .NET implementation must keep plugin input away from `System.Diagnostics.Process`, shell/interpreter invocation, dynamic code loading, and any comparable execution API. Tests may execute the APV test process itself, but must never execute fixture content.

### Dependency installation and build activity

APV MUST NOT invoke package managers or dependency/bootstrap tooling, including `dotnet restore`, `npm`, `npx`, `pnpm`, `yarn`, `pip`, `uv`, `cargo`, `go`, or platform equivalents, because a plugin configuration or file suggests it.

APV MUST NOT build, publish, restore, compile, transpile, bundle, or otherwise prepare plugin code for execution.

### Network and MCP runtime activity

APV MUST NOT:

- create an HTTP(S), SSE, WebSocket, stdio, socket, or MCP connection from a plugin-defined endpoint or server entry;
- follow redirects, perform DNS resolution, send headers, transmit environment values, authenticate, or complete an MCP handshake;
- retrieve remote JSON schemas, package content, dependencies, documentation, or credentials while validating a package.

Network-shaped values are parsed only as literal strings for deterministic syntax and conformance checks. A URL’s reachability, server behavior, TLS certificate, OAuth flow, and runtime security are outside APV V0.

### Filesystem writes and containment escapes

APV MUST NOT write, delete, rename, chmod, quarantine, install into, or otherwise modify the supplied plugin directory.

APV MUST NOT read a package-provided path that resolves outside the filesystem-resolved plugin root. This includes `..` traversal and symlink, junction, reparse-point, or equivalent escapes. The validator may inspect metadata required to establish a path's resolved location, but it must deny content access once the path is known to escape.

APV MUST NOT reinterpret opaque configuration strings—such as command arguments and environment values—as package paths unless the Agent Plugins specification explicitly defines that field as a path.

## Data handling and reporting

- Plugin content is untrusted and may contain malformed text, misleading instructions, or credential-like values.
- Findings include an actual value only when it is safe and necessary to explain a deterministic violation.
- APV must redact or omit secrets and secret-like values from console output, JSON reports, exceptions, logs, and test snapshots.
- APV must not claim that a package is safe, malware-free, secret-free, reachable, trusted, or certified.
- A conformance `VALID` result means only that the package satisfied APV's implemented deterministic rules; it is not an execution or security assurance.

The exact deterministic evidence policy for `APV-MCP-022` and `APV-MCP-023` is deferred to APV-6. Until that policy exists, APV must not introduce broad heuristic secret scanning.

## Resource limits and failure behavior

APV will use bounded reads and deterministic parsing. Numeric limits for file count, individual file size, aggregate bytes, nesting, and processing time are an APV-3 implementation decision.

Until those limits are implemented, no component may introduce unbounded recursive discovery, content reads, archive extraction, remote retrieval, or process execution as a workaround. A malformed, inaccessible, oversized, or containment-escaping input must produce a controlled finding or input error; it must not trigger a fallback action outside this contract.

## Architectural guardrails

The validation core must remain a pure inspection layer:

```text
explicit local directory
  → contained metadata/file reads
  → in-memory parsers and deterministic rules
  → findings/report
  → caller-controlled output
```

No arrow in this flow authorizes plugin runtime activation, external I/O driven by plugin content, or mutation of the package. A future API or web host may reuse the core only if it preserves this contract.

## Verification obligations for later tasks

| Control | Required evidence before claiming enforcement |
| --- | --- |
| No process/shell execution | Tests with hostile `command`, `args`, script, and executable fixtures; review that the core has no plugin-input path to process-launch APIs. |
| No network activity | Tests with endpoint fixtures plus architecture review that validation does not use plugin-derived network clients. |
| Contained read-only access | Traversal and symlink fixtures demonstrate no root-external content read; tests verify APV does not mutate fixtures. |
| Safe reporting | Fixtures containing credential-like data demonstrate masking/omission in findings, exceptions, and logs. |
| Bounded resource use | APV-3 tests demonstrate configured file/count/byte limits and controlled failure output. |

This APV-1.2 document establishes the contract. It does not falsely claim that these controls are implemented before APV-3 through APV-8 deliver their code and tests.

## Change control

Any proposal to execute plugin content, connect to plugin-defined endpoints, install dependencies, follow package paths outside root, or write inside a plugin package is a material product-scope and security change. It requires an explicit user decision, a new Jira task, a revised threat model, updated tests, and a vault decision before implementation.
