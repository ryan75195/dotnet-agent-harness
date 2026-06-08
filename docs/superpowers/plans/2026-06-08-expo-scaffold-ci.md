# Expo Scaffold CI Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bake a guardrails-only GitHub Actions `ci.yml` into the Expo template so every scaffolded app runs `npm run verify` on PRs and pushes to main, without affecting the harness repo.

**Architecture:** The workflow lives nested at `expo/templates/app/.github/workflows/ci.yml`. GitHub only runs workflows in `.github/workflows/` at a repo root, so this copy is inert in the harness repo and activates only when `new-app.ps1` copies the template to a new repo root. Docs (template README + CLAUDE.md) describe it and the EAS opt-in; the harness validation script asserts the file ships.

**Tech Stack:** GitHub Actions, Node 22, the existing `npm run verify` chain. PowerShell validation.

**Spec:** `docs/superpowers/specs/2026-06-08-expo-scaffold-ci-design.md`

**Working notes for the implementer:**
- The harness repo has no pre-commit hooks; commit directly to `main`.
- No source code or tests change in the template app itself — this is a workflow file + docs + one PowerShell assertion. Nothing to `npm run verify` here except confirming the template still scaffolds clean (the validation script does that in CI).
- The local validation script needs pwsh 7 to run; if only Windows PowerShell 5.1 is available locally, rely on CI (the `expo-scaffold-and-validate` job) as the authoritative run — same as prior tasks on this repo.

---

## File structure (additions/changes)

```
expo/templates/app/
  .github/workflows/ci.yml          ← NEW: guardrails CI (nested, inert in harness)
  README.md                         ← + CI badge + "Add an EAS build to CI" section
  CLAUDE.md                         ← lifecycle note + key-files entry
expo/template-tests/
  scaffold-and-validate.ps1         ← + assertion: ci.yml ships and has the right content
```

---

### Task 1: Add the CI workflow to the template

**Files:**
- Create: `expo/templates/app/.github/workflows/ci.yml`

- [ ] **Step 1: Create `expo/templates/app/.github/workflows/ci.yml`** with exactly:

```yaml
name: ci
on:
  pull_request:
  push:
    branches: [main]

jobs:
  verify:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'npm'
      - run: npm ci
      - run: npm run verify
```

- [ ] **Step 2: Confirm it is tracked and not ignored**

Run (from repo root):
```powershell
git check-ignore expo/templates/app/.github/workflows/ci.yml
```
Expected: NO output (exit 1) — meaning the file is NOT ignored. The template `.gitignore` ignores `node_modules/.expo/coverage/ios/android/.env*/*.p8`, none of which match `.github`. If it IS ignored, stop and report — something is wrong.

- [ ] **Step 3: Confirm `new-app.ps1` will copy `.github`**

Read `expo/new-app.ps1`. Verify the robocopy excludes are `node_modules .expo coverage .git` (the `/XD` list) — `.github` is NOT excluded, so it copies. Confirm the placeholder rename loop (`Get-ChildItem -Recurse -File`) would touch `ci.yml`, but since `ci.yml` contains none of the placeholder tokens (`AppTemplate`, `app-template`, `apptemplate`, `com.example.apptemplate`), it is copied byte-for-byte. No change to `new-app.ps1` is needed. State this confirmation in your report.

- [ ] **Step 4: Commit**

```powershell
git add expo
git commit -m "Add guardrails CI workflow to expo template"
```

---

### Task 2: Assert the workflow ships, in template validation

**Files:**
- Modify: `expo/template-tests/scaffold-and-validate.ps1`

The validation script scaffolds a fresh app and runs the guardrail set. Add a structural assertion that the scaffold contains a correct `ci.yml`. We use a presence + content check (not a full YAML parse) to avoid adding a YAML-parser dependency; the content check is stricter in practice — it verifies the workflow actually wires `npm ci` + `npm run verify`, not just that it is well-formed YAML.

- [ ] **Step 1: Insert the assertion**

In `expo/template-tests/scaffold-and-validate.ps1`, inside the `try { ... }` block, immediately AFTER the "Partial auth config must fail" block (the one ending with `if ($partialExit -ne 7) { throw ... }` on the line before `}` that closes the `try`), and BEFORE that closing `}`, insert:

```powershell
    Write-Host "CI workflow must ship in the scaffold..."
    $ciPath = Join-Path $scaffoldDir '.github\workflows\ci.yml'
    if (-not (Test-Path $ciPath)) { throw "ci.yml missing from scaffold at $ciPath" }
    $ciText = Get-Content $ciPath -Raw
    if ($ciText -notmatch 'name:\s*ci') { throw 'ci.yml missing the workflow name' }
    if ($ciText -notmatch 'npm ci') { throw 'ci.yml does not run npm ci' }
    if ($ciText -notmatch 'npm run verify') { throw 'ci.yml does not run npm run verify' }
```

Note: `$scaffoldDir` and the `try` block already exist in the script (the script `Push-Location $scaffoldDir` earlier, but this assertion uses the absolute `$scaffoldDir` path, which is correct regardless of the current location). Place it as the last assertion inside `try`.

- [ ] **Step 2: Syntax-check the script parses**

Run (from repo root, works in Windows PowerShell 5.1 or pwsh 7):
```powershell
$null = [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path .\expo\template-tests\scaffold-and-validate.ps1), [ref]$null, [ref]$null); if ($?) { 'parse ok' }
```
Expected: `parse ok` (no parser errors). This confirms the inserted PowerShell is syntactically valid without needing to execute the full scaffold (which requires pwsh 7).

- [ ] **Step 3: Commit**

```powershell
git add expo
git commit -m "Assert CI workflow ships in template validation"
```

---

### Task 3: Document CI in the template README and CLAUDE.md, then verify via CI

**Files:**
- Modify: `expo/templates/app/README.md`
- Modify: `expo/templates/app/CLAUDE.md`

- [ ] **Step 1: Add the badge + EAS opt-in to `README.md`**

Replace the top of `expo/templates/app/README.md` (lines 1–5, the title + intro paragraph) with:

```markdown
# AppTemplate

![CI](https://github.com/YOUR_GITHUB_USER/YOUR_REPO/actions/workflows/ci.yml/badge.svg)

Expo app scaffolded from the agent-harness expo template: error-severity
guardrails for agent-driven development plus a staged, resumable iOS App
Store submission workflow.

After `gh repo create`, replace `YOUR_GITHUB_USER/YOUR_REPO` in the badge above
with your repository path.
```

Then, at the END of `README.md` (after the existing "App Store submission" section), append:

```markdown
## Continuous integration

`.github/workflows/ci.yml` runs the full guardrail set (`npm run verify`) on
every pull request and on pushes to `main` — the same checks the local
`.githooks/pre-commit` runs, enforced on GitHub's runners so they cannot be
skipped with `--no-verify`.

### Add an EAS build to CI (optional)

To also build on CI, add an `EXPO_TOKEN` repository secret (from
`expo.dev` → Account → Access tokens) and append this job to `ci.yml`:

```yaml
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '22', cache: 'npm' }
      - run: npm ci
      - uses: expo/expo-github-action@v8
        with:
          eas-version: latest
          token: ${{ secrets.EXPO_TOKEN }}
      - run: eas build --platform ios --profile preview --non-interactive
```
```

Note: the inner ```yaml fence inside a markdown file is fine — it renders as a nested code block. Ensure the outer section is plain markdown text, not itself fenced.

- [ ] **Step 2: Update `CLAUDE.md`**

In `expo/templates/app/CLAUDE.md`, in the "Development lifecycle" section, find step 5 (the PR step). After the numbered list (after the line about squash merge / direct-edits-blocked), add this sentence as its own paragraph:

```markdown
CI (`.github/workflows/ci.yml`) re-runs `npm run verify` on every PR and on
pushes to `main` — the same gate the pre-commit hook enforces, now on the
server.
```

To locate it precisely: it goes immediately after the existing paragraph that begins "Direct edits and commits to `main` are blocked." and before the "## Code style" heading.

Then in the "## Key files" list at the bottom of CLAUDE.md, add this entry after the `.dependency-cruiser.cjs` line:

```markdown
- `.github/workflows/ci.yml` — server-side guardrail enforcement on PRs
```

- [ ] **Step 3: Commit**

```powershell
git add expo
git commit -m "Document CI workflow in template README and CLAUDE.md"
```

- [ ] **Step 4: Push and watch CI**

```powershell
git push
gh run list --limit 1
```
Then `gh run watch <id> --exit-status` (get `<id>` from the list). Report the final conclusion of all jobs: `scaffold-and-test (cli)`, `scaffold-and-test (etl-api)`, and `expo-scaffold-and-validate`. ALL must be green — the `expo-scaffold-and-validate` job now also runs the new ci.yml-ships assertion.

- [ ] **Step 5: Confirm the nested workflow stays inert in the harness repo**

After the push, run:
```powershell
gh workflow list
```
Expected: only the harness's own workflow(s) (e.g. `template-ci`) appear — NOT a workflow named `ci` from the nested template file. This confirms GitHub ignores the nested `expo/templates/app/.github/workflows/ci.yml` in the harness repo. Report what `gh workflow list` shows.

---

## Self-review notes (already applied)

- **Spec coverage:** nested ci.yml (T1); README badge + EAS opt-in (T3 Step 1); CLAUDE.md lifecycle + key-files (T3 Step 2); validation assertion (T2); CI verification + inertness confirmation (T3 Steps 4–5). The spec's "parse as valid YAML" is implemented as a presence+content check (T2) — a deliberate, stricter, dependency-free substitute; noted here so it is not mistaken for a gap.
- **Placeholder scan:** none. The badge `YOUR_GITHUB_USER/YOUR_REPO` is an intentional user-facing placeholder per the spec, not a plan placeholder, and the README text instructs the user to replace it.
- **Consistency:** the workflow name `ci`, the path `.github/workflows/ci.yml`, and the commands `npm ci` / `npm run verify` are identical across the workflow file, the validation assertion, the README, and CLAUDE.md.
