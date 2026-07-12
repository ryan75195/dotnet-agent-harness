---
name: new-dotnet-etl-api
description: Use when creating a new .NET ETL/API service from the agent-harness template ("new api", "new etl service", "new backend service"). Scaffolds, stamps provenance, and verifies guardrails.
---

# New .NET ETL API Project

Scaffold a .NET 10 ETL/API solution by delegating to the standalone `new-project` CLI
(the single source of truth for scaffold -> stamp -> setup -> verify).

## Inputs

- **Name** (required): PascalCase solution name, e.g. `OrdersEtl`. Ask if not given.
- **Destination** (optional): defaults to `<cwd>/<Name>`.

## Steps

1. **Resolve the harness repo:**
   ```powershell
   $repo = & "${CLAUDE_PLUGIN_ROOT}/scripts/resolve-repo.ps1" | Select-Object -Last 1
   ```
2. **Run the CLI** (omit `-Destination` when defaulted):
   ```powershell
   & "$repo/new-project.ps1" dotnet-etl-api <Name> -Destination <dir>
   ```
   This installs the templates, scaffolds, stamps provenance, runs setup, and verifies
   (`dotnet build` + `dotnet test`). If it prints a git-identity error, relay the fix and
   stop until the user configures git, then re-run.
3. **Report** what the CLI printed: the project path, the development lifecycle from the
   project's CLAUDE.md (issue -> feat branch -> commit -> PR), and that analyzers fire at
   error severity.

Never hand-edit `.harness.json` — the CLI writes it.
