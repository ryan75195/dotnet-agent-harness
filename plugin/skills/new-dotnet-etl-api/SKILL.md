---
name: new-dotnet-etl-api
description: Use when creating a new .NET ETL/API service from the agent-harness template ("new api", "new etl service", "new backend service"). Scaffolds, stamps provenance, and verifies guardrails.
---

# New .NET ETL API Project

Scaffold a .NET 10 ETL/API solution from the agent-harness `etl-api` template (Roslyn analyzers + architecture tests at error severity, Claude and Codex agent configs).

## Inputs

- **Name** (required): PascalCase solution name, e.g. `OrdersEtl`. Ask if not given.
- **Destination** (optional): defaults to `<cwd>/<Name>`.

## Steps

1. **Resolve the harness repo:**
   ```powershell
   $repo = & "${CLAUDE_PLUGIN_ROOT}/scripts/resolve-repo.ps1" | Select-Object -Last 1
   ```
2. **Install the templates** (idempotent):
   ```powershell
   dotnet new install "$repo/dotnet" --force
   ```
3. **Scaffold:**
   ```powershell
   dotnet new etl-api -n <Name> -o <dir>
   ```
4. **Stamp provenance:**
   ```powershell
   & "${CLAUDE_PLUGIN_ROOT}/scripts/write-stamp.ps1" -ProjectDir <dir> -Template etl-api -ProjectName <Name> -RepoPath $repo
   ```
5. **Run setup** (git init, hooks):
   ```powershell
   cd <dir>
   .\setup.ps1
   ```
   If setup already created the initial commit before the stamp existed, commit the stamp with `git add .harness.json; git commit --no-verify -m "Add harness provenance stamp"`.
6. **Verify before claiming success:**
   ```powershell
   dotnet build --no-incremental
   dotnet test --no-build --verbosity minimal
   ```
7. **Report:** project path, the development lifecycle from the project's CLAUDE.md (issue → feat branch → commit → PR), and that analyzers fire at error severity.

Never hand-edit `.harness.json`.
