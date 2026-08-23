---
task: APV-2.4
status: approved-contract
verified: 2026-08-23
---

# APV Fixture and Test-Helper Contract

## Purpose and scope

This contract defines how future APV tests express package inputs and expected
validation reports without turning fixtures into runnable plugins. It applies
to the target test boundaries in [solution-boundaries.md](solution-boundaries.md),
the report model in [validation-model.md](validation-model.md), and the
registry contract in
[rule-registry-and-specification-references.md](../specification/rule-registry-and-specification-references.md).

It does not create `tests/`, a test framework, a filesystem abstraction, helper
code, fixture directories, symlinks, temporary files, or snapshots. Those are
created only by the later implementation task that needs them.

## Fixture safety invariant

A fixture is untrusted, inert test data. A test may read its declared text and
configuration bytes to pass them to the Core package-reader seam, but it must
never execute, import, install, build, connect to, or otherwise activate the
fixture.

The following actions are prohibited for all fixture helpers and tests:

- launching a fixture executable, shell, script, MCP server, package manager,
  interpreter, or build tool;
- opening a network connection, resolving a remote endpoint, or dereferencing
  URLs from a fixture or expected reference;
- writing into the checked-in fixture source tree or accepting a fixture path
  outside the test-controlled fixture root;
- treating a hostile command string, credential-like value, traversal path, or
  malformed JSON/YAML as an instruction rather than inert input data.

This is a test-level reinforcement of the production never-execute contract;
passing tests must not depend on a plugin being runnable.

## Target fixture case model

The names below are target test-contract names only. Their concrete C# types,
on-disk metadata syntax, and assertion-library integration remain deferred.

| Contract | Required data | Purpose |
| --- | --- | --- |
| `FixtureCase` | `FixtureId`, `Intent`, `PackageTree`, `ExpectedReport` | One named package scenario and its semantic expectation. |
| `PackageTree` | Relative file/directory entries and their inert text bytes or an explicit synthetic filesystem condition | Describes the selected package root without a host-specific absolute path. |
| `ExpectedReport` | `OverallStatus`, Manifest/Skills/MCP statuses, entry counts where evaluated, ordered expected findings | The Core outcome to assert. |
| `ExpectedFinding` | `RuleId`, `Level`, `Component`, optional package-relative `FilePath`, and expected source reference identity | Stable semantic assertion; it does not depend on rendered prose. |
| `SyntheticFilesystemCondition` | Condition kind, affected package-relative path, expected boundary/result | Isolates a filesystem behaviour that cannot be represented as ordinary inert text. |
| `MaterializedFixture` | Test-controlled temporary root and explicit cleanup ownership | A disposable copy used only when a real filesystem test is necessary. |

`FixtureId` is test-only and uses the pattern `FX-<surface>-<scenario>`, for
example `FX-MANIFEST-MALFORMED-JSON`. It is not a public diagnostic, Rule ID,
or compatibility promise. `Intent` states the one behaviour the case protects,
so a fixture cannot silently grow into an unrelated end-to-end test.

## Required expectation semantics

Every fixture case must assert these report fields explicitly:

1. the overall status;
2. Manifest, Skills, and MCP component status, including `Absent` and
   `NotEvaluated` display states;
3. Skills/MCP entry counts whenever that component was evaluated;
4. the complete ordered set of expected finding identities; and
5. for every finding, its Rule ID, level, owning component, location when
   applicable, and resolved specification-source identity.

Expected findings are compared semantically. A test must not rely on a
human-readable renderer, an absolute temporary path, exception text, a network
response, locale, current time, or the order of host filesystem enumeration.
Prose templates may be tested only when APV-7 makes them public output
contract; they are not a substitute for rule/status assertions.

An expectation is complete by default: an unexpected error, missing finding,
changed finding order, wrong registry source, or incompatible count fails the
case. A deliberately partial assertion must name the single invariant it is
isolating and may not be used for conformance-boundary coverage.

## Fixture classes and planned placement

The intended future location is `tests/fixtures/`, as reserved by APV-2.1.
Each case is source-controlled, self-contained, and contains only its package
tree plus test expectation metadata. A test runner must not discover arbitrary
directories as fixtures.

| Class | Use | Example protected outcome |
| --- | --- | --- |
| Baseline | Minimal accepted portable package | `Valid` with optional components `Absent` |
| Fatal manifest | Root/JSON/schema failure | Manifest `Invalid`; dependent components `NotEvaluated` |
| Continue-warning | Explicit non-fatal manifest exception | Manifest valid plus warning, with continued evaluation |
| Component | Wrong fixed Skills/MCP location or invalid top-level MCP | Only the affected component is `Invalid` |
| Entry | One invalid Skill or MCP server among valid siblings | Parent `Partial`, sibling entries retained |
| Applicability | Known vendor-only package without portable root manifest | `NotApplicable` and `APV-FORMAT-001` only |
| Containment | Symlink/traversal/invalid relative path condition | The rule's narrow failure boundary and no outside-root read |

No fixture is added merely to increase count. Each new registry rule or
failure-boundary branch needs a minimal case tied to its `RuleId` and an
explicit expected report.

## Filesystem and temporary-root helpers

Ordinary rules should use an in-memory or controlled package-reader seam so
they do not depend on host disk layout. A real temporary root is allowed only
for behaviour that requires it, such as physical canonicalization or symlink
containment, and must follow all of these rules:

- The helper creates a unique child under the test runner's temporary area and
  records that exact child as `MaterializedFixture` ownership.
- It copies from immutable fixture input to the unique child; it never mutates
  the checked-in fixture case.
- Cleanup may remove only that recorded temporary child. It must not use a
  broad temporary directory, a package root supplied by the fixture, a glob, or
  an unresolved path as a cleanup target.
- A synthetic outside-root target, when needed for a containment test, is a
  harmless text file under the same test-controlled temporary parent. It is
  never a user path, home directory, repository path, or network mount.
- If the host cannot create the filesystem feature being integrated-tested,
  a controlled reader double still tests the Core's boundary semantics. The
  test suite must not silently drop the corresponding Rule ID coverage.

Future helper names may be `FixturePackageBuilder`, `ControlledPackageReader`,
and `FixtureReportAssertions`. They are not an authorization to introduce a
generic dependency-injection layer or a runtime file service.

## Test-layer responsibilities

| Test layer | Owns | Must not own |
| --- | --- | --- |
| Core contract/unit tests | Immutable report, registry, ordering, aggregation, and assertion-helper semantics | CLI parsing, console text, or actual package execution |
| Core fixture tests | Rule behaviour, failure boundaries, safe path handling, and report values through controlled inputs | Renderer snapshots, remote dependencies, or vendor validation |
| CLI tests | Invocation errors, mapping of an already-produced Core report, rendering, and exit codes | Duplicate manifest/Skill/MCP rule implementations |

The CLI may consume a prebuilt `ValidationReport` in its own tests. It must not
need a runnable fixture plugin to verify presentation or exit-code mapping.

## Determinism and review gates

- Fixture file paths use normalized package-relative notation in expectations;
  tests derive any temporary absolute path only internally.
- Fixture discovery order and finding order are asserted according to the Core
  model's deterministic ordering contract.
- Credential-like values are represented only as inert, minimal text needed
  for the specific rule. Test output must redact them just as production
  findings do.
- A fixture change that changes a Rule ID, status, count, or source reference
  requires review of the matching registry/matrix contract and its expected
  report; changing a snapshot solely to accept a different renderer is not
  sufficient.
- Tests must run offline and without external services. A passing test suite is
  evidence of static validation behaviour only, never of MCP connectivity or
  plugin runtime compatibility.

## Deferred implementation

- The first Core implementation task selects the test framework, fixture
  metadata syntax, actual directory layout, test runner, and temporary-root
  APIs with current .NET evidence.
- APV-3 defines the package-reader seam and containment implementation used by
  the controlled reader and materialized-fixture tests.
- APV-4 through APV-6 add only the fixtures needed by their rule scopes.
- APV-7 may add renderer-specific golden tests without replacing the semantic
  fixture assertions in this contract.
