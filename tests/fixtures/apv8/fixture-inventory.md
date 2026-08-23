# APV-8 regression fixture inventory

All files in this tree are inert UTF-8 package data. No fixture is a runnable
plugin, command, script, or MCP server.

| Fixture | Expected overall | Focus |
| --- | --- | --- |
| `minimal-valid` | `Valid` | Canonical minimal manifest; optional components absent |
| `full-valid` | `Valid` | Valid manifest, Skill, stdio, streamable-http, and sse |
| `invalid-manifest` | `Invalid` | Fatal manifest stops Skills/MCP discovery |
| `invalid-skill` | `Invalid` | Invalid Skill leaves valid MCP visible |
| `invalid-mcp` | `Invalid` | Invalid top-level MCP leaves valid Skill visible |
| `version-mismatch` | `Invalid` | MCP schema mismatch disables MCP only |
| `path-traversal` | `Invalid` | MCP cwd traversal invalidates one server entry |
| `secret-header` | `Invalid` | Deterministic credential header finding |
