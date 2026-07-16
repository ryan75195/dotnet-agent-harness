---
name: new-dotnet-durable
description: Use when creating a new Azure Durable Functions app from the agent-harness template ("new durable functions app", "new durable workflow", "new orchestration", "new dotnet durable"). Scaffolds via the new-project CLI, which stamps provenance and verifies guardrails.
---

# New .NET Azure Durable Functions Project

Scaffold a .NET 10 Azure Durable Functions app (isolated worker, Azure Storage backend) by
delegating to the standalone `new-project` CLI (the single source of truth for scaffold ->
stamp -> setup -> verify).

## Inputs

- **Name** (required): PascalCase solution name, e.g. `AgentRunner`. Ask if not given.
- **Destination** (optional): defaults to `<cwd>/<Name>`.

## Steps

1. **Resolve the harness repo:**
   ```powershell
   $repo = & "${CLAUDE_PLUGIN_ROOT}/scripts/resolve-repo.ps1" | Select-Object -Last 1
   ```
2. **Run the CLI** (omit `-Destination` when defaulted):
   ```powershell
   & "$repo/new-project.ps1" dotnet-durable <Name> -Destination <dir>
   ```
   This installs the templates, scaffolds, stamps provenance, runs setup, and verifies
   (`dotnet build` + unit/architecture/analyzer tests). If it prints a git-identity error,
   relay the fix and stop until the user configures git, then re-run. If it notes that
   Azurite is missing, relay that too — it is a warning, not a failure: Azurite is needed
   only for local `func start` runs and the integration tests, not for the scaffold or the
   pre-commit gate.
3. **Report** what the CLI printed: the project path, that the app ships a webhook-started
   orchestration with fan-out/fan-in, an external-event-vs-timer race, and a durable entity,
   and the development lifecycle from the project's CLAUDE.md (issue -> feat branch ->
   commit -> PR).

Never hand-edit `.harness.json` — the CLI writes it.
