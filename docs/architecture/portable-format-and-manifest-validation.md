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
| Recognized Codex-only package with no root portable manifest | `NotEvaluated` / overall `NotApplicable` | Blocked; vendor format is not validated |

## Format decision

1. APV first reads only root `plugin.json` through the contained reader.
2. A readable root manifest is a portable candidate, including an unsupported
   `$schema`, which is an `Invalid` portable claim rather than `NotApplicable`.
3. When root `plugin.json` is absent, APV checks the explicit Codex marker
   `.codex-plugin/plugin.json`. If it is contained and readable, APV reports
   `APV-FORMAT-001` at `Info` level and returns `NotApplicable`.
4. A package without either marker is invalid with `APV-PACKAGE-001`.

The detector does not parse or validate Codex configuration and does not make
any vendor conformance or security claim.

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
required name, unsupported schema, invalid name/metadata shapes, a Codex-only
package, and a package with no known format marker.

The Release test command completes with 19 passed, 0 failed, 0 skipped. All
fixtures remain inert data and only the APV test process is executed.

## Deferred work

- APV-5/APV-6 perform component discovery only when this result allows it.
- The current rule map covers APV-4's emitted findings; later tasks extend the
  complete registry without changing existing Rule IDs or references.
- CLI report rendering and exit codes remain outside APV-4.
