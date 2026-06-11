---
name: new-dotnet-cli
description: Use when creating a new .NET CLI/console project from the agent-harness template ("new cli tool", "new console app", "new dotnet project"). Scaffolds, stamps provenance, and verifies guardrails.
---

# New .NET CLI Project

Scaffold a .NET 10 console solution from the agent-harness `cli` template (15 Roslyn analyzers + architecture tests at error severity).

## Inputs

- **Name** (required): PascalCase solution name, e.g. `MyTool`. Ask if not given.
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
   dotnet new cli -n <Name> -o <dir>
   ```
4. **Stamp provenance:**
   ```powershell
   & "${CLAUDE_PLUGIN_ROOT}/scripts/write-stamp.ps1" -ProjectDir <dir> -Template cli -ProjectName <Name> -RepoPath $repo
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
