---
task: APV-9
status: implemented
verified: 2026-08-23
---

# APV CI and Open-Source Delivery

`.github/workflows/build-test.yml` runs on pushes to `main` and pull requests
with least-privilege `contents: read`. On a fresh `ubuntu-latest` checkout it:

1. checks out source;
2. installs the SDK pinned by `global.json` via `actions/setup-dotnet`;
3. restores the solution;
4. builds the Release solution without a second restore; and
5. runs the full Release test suite without a second build/restore.

The workflow neither runs an APV fixture as a plugin nor connects to a
plugin-provided endpoint. It runs only the repository's .NET build/test tools.

The public README is an implementation-facing product entry point. It states
the supported portable Agent Plugins 1.0.0 scope, CLI command/exit codes,
verified valid/invalid output, architecture boundary, rule-catalog links, and
hard safety/limitation language. It differentiates a recognized vendor-only
package (`NOT_APPLICABLE`) from portable conformance and explicitly rejects
security-certification and malware-scanner claims.

## Local verification

```sh
dotnet restore AgentPluginValidator.sln --nologo
dotnet build AgentPluginValidator.sln --configuration Release --no-restore --nologo
dotnet test AgentPluginValidator.sln --configuration Release --no-build --no-restore --nologo
```

The build/test commands were run successfully before the APV-9 delivery. A
GitHub-hosted workflow run remains external evidence and is not claimed until
GitHub executes the pushed workflow.
