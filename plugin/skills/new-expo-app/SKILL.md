---
name: new-expo-app
description: Use when creating a new Expo/React Native app from the agent-harness template ("new expo app", "new mobile app", "scaffold an app with payments"). Scaffolds, stamps provenance, and verifies guardrails.
---

# New Expo App

Scaffold a new Expo app from the agent-harness `expo/templates/app` template.

## Inputs

- **Name** (required): PascalCase, must match `^[A-Z][A-Za-z0-9]*$` (e.g. `MyApp`). Ask if not given.
- **BundleId** (optional): defaults to `com.example.<name lowercase>`. Ask if the user mentioned shipping to the App Store.
- **Destination** (optional): defaults to `<cwd>/<Name>`.

## Steps

Run all PowerShell via the PowerShell tool; `${CLAUDE_PLUGIN_ROOT}` is this plugin's root.

1. **Resolve the harness repo:**
   ```powershell
   $repo = & "${CLAUDE_PLUGIN_ROOT}/scripts/resolve-repo.ps1" | Select-Object -Last 1
   ```
2. **Scaffold** (omit -BundleId / -Destination when defaulted):
   ```powershell
   & "$repo/expo/new-app.ps1" -Name <Name> -BundleId <bundleId> -Destination <dir>
   ```
3. **Stamp provenance BEFORE setup** so the initial commit includes it:
   ```powershell
   & "${CLAUDE_PLUGIN_ROOT}/scripts/write-stamp.ps1" -ProjectDir <dir> -Template expo-app -ProjectName <Name> -RepoPath $repo -BundleId <bundleId>
   ```
4. **Run setup** (npm install, git init, activates .githooks, initial commit):
   ```powershell
   cd <dir>
   .\setup.ps1
   ```
   If it fails on git identity, relay the fix it prints and stop until the user configures it.
5. **Verify before claiming success:**
   ```powershell
   npm run verify
   ```
   Every gate must pass. Fix and re-run if not.
6. **Report:** project path, active guardrails (no comments, strict TS, layer rules, coverage), and next steps: `gh repo create`, then read `CLAUDE.md` and `SUBMISSION.md`.

Never hand-edit `.harness.json` — it is written here and updated only by the harness-update skill.
