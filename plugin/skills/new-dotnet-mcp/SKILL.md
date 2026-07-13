---
name: new-dotnet-mcp
description: Use when creating a new .NET MCP server from the agent-harness template ("new mcp server", "new model context protocol server", "new dotnet mcp"). Scaffolds via the new-project CLI, which stamps provenance and verifies guardrails.
---

# New .NET MCP Server Project

Scaffold a .NET 10 MCP server (HTTP/streamable, ModelContextProtocol SDK) by delegating
to the standalone `new-project` CLI (the single source of truth for scaffold -> stamp ->
setup -> verify).

## Inputs

- **Name** (required): PascalCase solution name, e.g. `WeatherMcp`. Ask if not given.
- **Destination** (optional): defaults to `<cwd>/<Name>`.

## Steps

1. **Resolve the harness repo:**
   ```powershell
   $repo = & "${CLAUDE_PLUGIN_ROOT}/scripts/resolve-repo.ps1" | Select-Object -Last 1
   ```
2. **Run the CLI** (omit `-Destination` when defaulted):
   ```powershell
   & "$repo/new-project.ps1" dotnet-mcp <Name> -Destination <dir>
   ```
   This installs the templates, scaffolds, stamps provenance, runs setup, and verifies
   (`dotnet build` + `dotnet test`, including the MCP protocol integration test). If it
   prints a git-identity error, relay the fix and stop until the user configures git,
   then re-run.
3. **Report** what the CLI printed: the project path, that the server speaks MCP over
   HTTP with a sample tool/resource/prompt, and the development lifecycle from the
   project's CLAUDE.md (issue -> feat branch -> commit -> PR).

Never hand-edit `.harness.json` — the CLI writes it.
