---
task: APV-4
status: implemented
specification: Agent Plugins 1.0.0
verified: 2026-08-23
---

# APV Portable Format and Manifest Validation

## Delivered scope

`PortableManifestValidator` is a Core-only, static validator built on
`SafePackageReader`. It produces a package/manifest result and explainable
findings; it does not parse CLI arguments, discover Skills/MCP components,
connect to a vendor, fetch a schema, or execute package content.

| Result | Manifest status | Component discovery |
| --- | --- | --- |
| Valid portable manifest | `Valid` | Allowed for APV-5/APV-6, but not performed by APV-4 |
| Fatal root/manifest failure | `Invalid` | Blocked |
| Recognized vendor-only package with no portable claim | `NotEvaluated` / overall `NotApplicable` | Blocked; vendor format is not validated |

## Format decision

1. APV first reads only root `plugin.json` through the contained reader.
2. A root manifest with the canonical Agent Plugins 1.0.0 `$schema` is a
   portable candidate. An explicit but unsupported/non-canonical `$schema`
   remains an `Invalid` portable claim rather than `NotApplicable`.
3. A readable JSON-object root manifest without `$schema` is classified as the
   documented Copilot default format and reports `APV-FORMAT-001` at `Info`
   level with `NotApplicable`.
4. When root `plugin.json` is absent, APV recognizes the contained marker paths
   `.codex-plugin/plugin.json`, `.claude-plugin/plugin.json`, and
   `.plugin/plugin.json` as Codex, Claude, and legacy OpenPlugin formats.
   Each returns `NotApplicable` with `APV-FORMAT-001`.
5. Invalid JSON, a non-object root manifest, an explicit malformed `$schema`,
   and a package with no known marker remain `Invalid` under the relevant
   package/manifest rule.

The detector only performs bounded static reads to identify markers. It does
not parse or validate vendor configuration and does not make any vendor
conformance or security claim.

## Manifest rules

APV implements these Rule IDs with static `SpecificationReference` metadata:

| Rule IDs | Behaviour |
| --- | --- |
| `APV-PACKAGE-001`, `APV-PATH-001` | Missing/unreadable root manifest or root containment failure is fatal. |
| `APV-MANIFEST-001` | JSON and top-level-object requirement. |
| `APV-MANIFEST-002` | Exact local canonical 1.0.0 `$schema`; no schema download. |
| `APV-MANIFEST-003`, `004` | Required non-empty name and portable name constraints. |
| `APV-MANIFEST-005` | JSON types of metadata, `keywords`, and closed string-only `author`. SemVer, URL, email, and SPDX semantic checks are intentionally not added. |
| `APV-MANIFEST-006` | Every unknown top-level field is reported then ignored as a warning. |
| `APV-MANIFEST-007` | A non-object `extensions` is reported and ignored as a warning. Implemented extension namespace values are not interpreted. |

Any `Error` is fatal to the portable package and sets
`ComponentDiscoveryAllowed` to `false`. Warnings alone leave the manifest
valid. The implementation uses a finite in-process rule map; references are
resolved locally and URLs/URIs are never dereferenced.

## Tests and fixtures

`tests/fixtures/apv4` and `PortableManifestValidatorTests` cover valid full
metadata, warning-only unknown/extensions behaviour, malformed JSON, missing
required name, unsupported schema, invalid name/metadata shapes, Codex,
Copilot, Claude, and legacy OpenPlugin formats, and a package with no known
format marker.

The Release test command completes with 19 passed, 0 failed, 0 skipped. All
fixtures remain inert data and only the APV test process is executed.

## Deferred work

- APV-5/APV-6 perform component discovery only when this result allows it.
- The current rule map covers APV-4's emitted findings; later tasks extend the
  complete registry without changing existing Rule IDs or references.
- CLI report rendering and exit codes remain outside APV-4.
