---
task: APV-3
status: implemented
verified: 2026-08-23
---

# APV Safe Package Reader

## Delivered scope

APV-3 introduces the first implementation projects and a read-only Core package
reader:

```text
AgentPluginValidator.sln
global.json
src/AgentPluginValidator.Core
tests/AgentPluginValidator.Core.Tests
tests/fixtures/apv3
```

The CLI project remains intentionally uncreated. The reader is implemented in
`AgentPluginValidator.Core.PackageIntake` and is independent of console,
command-line parsing, rendering, network, and process APIs.

`global.json` records the verified local SDK baseline (`10.0.400`) while
allowing a later feature-band SDK to service the same `net10.0` project.

## Public Core contract

| Type | Responsibility |
| --- | --- |
| `SafePackageReader` | Resolves one explicit plugin root and reads contained UTF-8 text only. |
| `PackageReaderOptions` | Immutable per-file and aggregate content limits. |
| `PackageReadResult<T>` | Controlled success/failure result; routine bad input does not escape as an unhandled exception. |
| `PackageReadFailure` | Stable reader-level failure code, safe message, and package-relative requested path when available. |
| `PackageReadFailureCode` | Root, malformed path, containment/link, file-kind, limit, encoding, and I/O classifications. |

Reader failure codes are infrastructure facts, not user-facing conformance
findings yet. APV-4 through APV-6 will map a specific failed read to the
registered APV Rule ID and its context-specific boundary. The reader does not
invent validation findings or a CLI status.

## Root and containment behaviour

1. `TryCreate` accepts only an explicit existing directory and resolves a root
   link to its final directory target before setting the package boundary.
2. Every requested path must be non-empty and relative. Absolute paths, empty
   segments, `.` segments, invalid filename characters, and `..` traversal are
   rejected before content access.
3. Every existing path segment is inspected for a link/reparse-point target.
   A target outside the resolved root returns `SymlinkEscapesRoot`; its content
   is never opened.
4. A final target must be a contained regular file. A directory, absent path,
   unresolved link, malformed path, or I/O problem returns a controlled
   `PackageReadFailure`.
5. The reader opens only the resolved, contained file with read access. It
   does not enumerate arbitrary roots, write fixture/package data, execute
   commands, load assemblies, or use network APIs.

The path policy recognizes both `/` and `\\` as separators before validating
portable relative paths, so a host-specific separator cannot bypass traversal
checks.

## Bounded read policy

| Limit | Default | Enforcement |
| --- | --- | --- |
| Per file | 1 MiB (`1,048,576` bytes) | Checked from the opened stream length before allocation/read. |
| Aggregate per reader/session | 10 MiB (`10,485,760` bytes) | Checked before each contained file is read; successful and interrupted reads account for bytes already consumed. |
| In-memory allocation | At most the configured per-file maximum, capped at `Int32.MaxValue` by options validation | No unbounded `ReadAllText` or recursive content scan. |

Limits are constructor parameters for deterministic tests and future host
configuration. They do not come from a plugin package. A file that would pass
the per-file limit but exceed the session aggregate returns
`TotalContentLimitExceeded` without reading that next file.

## Test evidence

`AgentPluginValidator.Core.Tests` uses xUnit and copied inert fixtures under
`tests/fixtures/apv3`. Its 10 tests cover:

- contained UTF-8 read without fixture mutation;
- `../`, nested traversal, absolute, missing, and malformed path failures;
- root-link resolution plus file and intermediate-directory links escaping the
  package root, with no external content read;
- a contained internal symlink;
- per-file and aggregate content limits.

Symlink tests materialize a copy under a unique test temporary child and create
only harmless text targets under the same temporary parent. The helper deletes
only that exact parent in `Dispose`; checked-in fixtures are never changed.

## Security review boundary

Static review confirms the Core package-intake implementation contains no
`System.Diagnostics.Process`, shell, package-manager, HTTP client, socket, or
dynamic-loading API. `dotnet test` executes the APV test process only; it does
not execute any fixture or plugin-provided content.

## Deferred work

- APV-4 maps root/manifest reader failures into package/manifest findings and
  applies portable format detection.
- APV-5 and APV-6 add only fixed-location discovery through this reader and
  preserve the rule-specific component/entry boundaries.
- A CLI host, parser/schema packages, report renderer, and CLI tests remain
  outside APV-3.
