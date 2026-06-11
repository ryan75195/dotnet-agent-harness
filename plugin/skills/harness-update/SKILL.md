---
name: harness-update
description: Use when asked to update the harness, pull the latest template/guardrail changes, or sync a project with the agent-harness repo. Works in any project previously scaffolded from an agent-harness template.
---

# Harness Update

Pull the latest agent-harness template changes into this project. Scripts do the deterministic work; you do the judgment. Updates touch ONLY harness-owned files (declared in the template's harness-manifest.json) — never the user's app code.

## Flow

### 1. Read the stamp

Read `.harness.json` in the project root.

**Missing stamp → backfill:** ask which template the project came from (cli / etl-api / expo-app) and roughly when it was scaffolded. Resolve the repo (step 2), find a plausible commit (`git -C $repo log --format='%H %ad' --date=short -- <templateDir>` around that date; when unsure use the OLDEST plausible commit — too-old means more files flagged for manual merge, which is safe; too-new silently skips updates). Then write the stamp with the plugin's write-stamp script pointed at that commit:
```powershell
git -C $repo checkout <commit> --quiet
& "${CLAUDE_PLUGIN_ROOT}/scripts/write-stamp.ps1" -ProjectDir . -Template <template> -ProjectName <name from the project's .sln/app.json> -RepoPath $repo
git -C $repo checkout main --quiet
```
For expo projects pass `-BundleId` read from `app.config.js`/`app.json`. Confirm the rename map in the written stamp matches reality (search the project for the renamed values).

### 2. Compute the update manifest

```powershell
$manifestPath = Join-Path $env:TEMP 'harness-update-manifest.json'
& "${CLAUDE_PLUGIN_ROOT}/scripts/get-update.ps1" -OutFile $manifestPath
```
Read the manifest. If `files` is empty, report "already up to date at <headCommit>" and stop.

### 3. Open the lifecycle (issue → branch)

This project's hooks enforce the lifecycle — follow it:
```powershell
gh issue create --title "Update harness to <first 7 chars of headCommit>" --body "Pull latest agent-harness template changes."
git checkout -b feat/<N>-harness-update
```

### 4. Apply the safe files

```powershell
& "${CLAUDE_PLUGIN_ROOT}/scripts/apply-update.ps1" -ManifestPath $manifestPath
```

### 5. Merge the flagged files

For each `modified`/`deleted` entry, get the three versions and merge:
```powershell
git -C <repoPath> show "<lastUpdateCommit>:<templatePath>"   # base
git -C <repoPath> show "<headCommit>:<templatePath>"          # incoming
```
plus the project's current file. Apply the stamp's renames mentally when comparing. Produce a merge that keeps the project's customizations AND adopts the template's improvements; explain each decision in one line. If a customization genuinely conflicts with the update's intent, ask the user. For `deleted` entries: delete the project file only if the project never customized it; otherwise ask.

### 6. Verify — the update is not done until the project's own gate passes

- expo: `npm run verify`
- dotnet: `dotnet build --no-incremental` then `dotnet test --no-build`

Fix failures before proceeding (a template update that breaks the project is a merge you got wrong in step 5 — revisit it, don't loosen the project).

### 7. Bump the stamp and ship

```powershell
$s = Get-Content .harness.json -Raw | ConvertFrom-Json
$s.lastUpdateCommit = '<headCommit>'
[IO.File]::WriteAllText((Join-Path (Get-Location) '.harness.json'), ($s | ConvertTo-Json -Depth 5), (New-Object System.Text.UTF8Encoding $false))
```
Commit everything (normal commit — the gate runs), open the PR per the project's CLAUDE.md lifecycle, and summarize: files applied, files merged (with one-line rationale each), files needing the user's decision.
