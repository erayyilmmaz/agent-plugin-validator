---
task: APV-5
status: implemented
verified: 2026-08-23
---

# APV Skill Discovery and Validation

`SkillValidator` validates Agent Skills only after portable-manifest validation
permits component discovery. It receives a `SafePackageReader`; it does not own
filesystem root resolution and never executes package content.

## Discovery boundary

- The only discovery location is `skills/<skill-name>/SKILL.md`.
- Only immediate child directories of the contained `skills/` directory are
  candidates. Nested `SKILL.md` files are ignored.
- An absent `skills/` directory yields `Absent`, not a finding.
- Reader containment, link, traversal, size, and decoding failures are surfaced
  as controlled `APV-SKILL-001` findings for the affected candidate.

## Frontmatter and semantic checks

The implementation uses a bounded scalar YAML-frontmatter mapping parser. It
requires opening/closing `---` delimiters, unique `key: value` entries, and
supports comments and quoted scalar values. It deliberately does not load YAML
tags, anchors, includes, or arbitrary object graphs.

| Rule | Check | Invalid-entry outcome |
| --- | --- | --- |
| `APV-SKILL-002` | YAML frontmatter is structurally parseable | Skip entry |
| `APV-SKILL-003` | `name` exactly matches immediate directory name | Skip entry |
| `APV-SKILL-004` | `name` is 1–64 lowercase ASCII letters/digits/hyphens | Skip entry |
| `APV-SKILL-005` | `description` is present and 1–1024 characters | Skip entry |

`SkillsValidationResult` reports `Valid`, `Invalid`, `Partial`, or `Absent`.
`Partial` requires at least one valid and at least one invalid discovered skill;
one invalid skill never prevents its siblings from being evaluated.

## Verification

Inert fixtures cover an absent `skills/` directory, malformed frontmatter,
invalid name, directory-name mismatch, missing description, a valid sibling,
and a nested ignored `SKILL.md`. Release test verification passed 22/22 tests.
