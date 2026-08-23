---
task: APV-7
status: implemented
verified: 2026-08-23
---

# APV Report Orchestration and CLI

`PluginValidator` is the Core-only orchestration entry point. It composes the
manifest, Skills, and MCP validators into one immutable `ValidationReport`;
the CLI only creates a reader, invokes it, renders the result, and maps its
overall status to an exit code.

## Aggregation

- `NOT_APPLICABLE` preserves the format finding and marks all portable
  components `NotEvaluated`.
- A fatal manifest error makes Manifest `Invalid`, leaves Skills/MCP
  `NotEvaluated`, and never starts component discovery.
- After a valid manifest, Skills and MCP are evaluated independently. Their
  findings and entry summaries remain visible even when the overall package is
  `Invalid`.
- Findings use deterministic component/rule/path ordering. The summary derives
  error/warning/info and component-status counts from report data.

## CLI contract

```text
agent-plugin-validator validate <plugin-directory> [--quiet|--ci]
```

- Human mode reports target, overall status, all component statuses, entry
  counts, severity counts, and each finding's rule, file, explanation, fix,
  and specification locator.
- `--quiet` writes no normal report; `--ci` writes one deterministic status
  line. Neither mode changes validation or exit behavior.
- Exit codes: `0` valid, `1` invalid, `2` input/usage error, `3` not
  applicable. No compatibility score or percentage is emitted.

## Verification

Core report tests cover valid, component-local invalid, fatal manifest, and
recognized vendor-only cases. CLI end-to-end tests cover human output, invalid
detail, not-applicable, usage/input errors, quiet, and CI modes. Release suite
passed 37/37 tests. The built executable emitted:

```text
STATUS=VALID ERRORS=0 WARNINGS=0 INFO=0 MANIFEST=VALID SKILLS=ABSENT MCP=ABSENT
```

for an inert valid fixture without executing package content.
