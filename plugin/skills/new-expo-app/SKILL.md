---
name: new-expo-app
description: Use when creating a new Expo/React Native app from the agent-harness template ("new expo app", "new mobile app", "scaffold an app with payments"). Scaffolds, stamps provenance, and verifies guardrails.
---

# New Expo App

Scaffold a new Expo app by delegating to the standalone `new-project` CLI, which is the
single source of truth for scaffold -> stamp -> setup -> verify.

## Inputs

- **Name** (required): PascalCase, `^[A-Z][A-Za-z0-9]*$` (e.g. `MyApp`). Ask if not given.
- **BundleId** (optional): ask if the user mentioned shipping to the App Store; otherwise
  omit and the default `com.example.<name>` is used.
- **Destination** (optional): defaults to `<cwd>/<Name>`.

## Steps

1. **Resolve the harness repo:**
   ```powershell
   $repo = & "${CLAUDE_PLUGIN_ROOT}/scripts/resolve-repo.ps1" | Select-Object -Last 1
   ```
2. **Run the CLI** (omit `-BundleId` / `-Destination` when defaulted):
   ```powershell
   & "$repo/new-project.ps1" expo <Name> -BundleId <bundleId> -Destination <dir>
   ```
   This scaffolds, stamps provenance, runs setup, and verifies. If it prints a
   git-identity error, relay the fix it shows and stop until the user configures git,
   then re-run.
3. **Report** what the CLI printed: the project path, active guardrails (no comments,
   strict TS, layer rules, coverage), and next steps: `gh repo create`, then read
   `CLAUDE.md` and `SUBMISSION.md`.

Never hand-edit `.harness.json` — the CLI writes it.
