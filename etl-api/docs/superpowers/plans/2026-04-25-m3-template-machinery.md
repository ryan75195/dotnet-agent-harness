# M3 Template Machinery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure this repo from "a working solution" into "a `dotnet new` template that scaffolds a working solution" — adding template manifest, post-scaffold setup script, READMEs, template validation tests, and CI.

**Architecture:** Move every existing repo file into a `content/` subdirectory; add `.template.config/template.json` at the root with a `sources` mapping pointing back to `content/`; add support files (setup.ps1, READMEs, template-tests, CI workflow) around it. After this milestone, `dotnet new install ./` followed by `dotnet new etl-api -n MyApp` produces a working scaffolded project.

**Tech Stack:** `dotnet new` template engine, PowerShell, GitHub Actions, bash hooks (in scaffolded output only).

**Spec:** `docs/superpowers/specs/2026-04-25-etl-api-template-extraction-design.md` (M3 section)
**Umbrella issue:** #13

---

## Decisions

- **`.template.config/` lives at the repo root** (per spec), with `sources` mapping pointing to `../content/`. This separates "template-repo concerns" from "templated content."
- **Template repo lifecycle is unenforced** after the move — `.githooks/` is now inside `content/`, so it won't fire in the template repo itself. Future template improvements happen on feat branches + PRs without local pre-commit, with template-CI as the safety net.
- **Windows-first.** `setup.ps1` only (no bash bootstrap). `template-ci.yml` runs on `windows-latest` only.
- **No NuGet packaging.** Install via `dotnet new install <git-url>` or `dotnet new install <local-path>`.

---

## File map

**Move (with `git mv`, preserves history):**
- `src/` → `content/src/`
- `tests/` → `content/tests/`
- `.githooks/` → `content/.githooks/`
- `.claude/` → `content/.claude/`
- `.editorconfig` → `content/.editorconfig`
- `.gitignore` → `content/.gitignore`
- `Directory.Build.props` → `content/Directory.Build.props`
- `GlobalRealEstate.slnx` → `content/GlobalRealEstate.slnx`
- `CLAUDE.md` → `content/CLAUDE.md`

**Stay at root:** `docs/`, `.git/`, `.vs/`

**Create:**
- `.template.config/template.json` — template manifest with `sources` mapping
- `content/setup.ps1` — post-scaffold setup script
- `content/README.md` — quickstart for scaffolded projects
- `template-tests/scaffold-and-build.ps1` — local template validation
- `.github/workflows/template-ci.yml` — CI runs scaffold + build + test on every push
- `README.md` (root) — template usage docs
- `.gitignore` (root) — template-repo concerns (`.vs/`, scratch dirs)

**Modify:**
- `content/CLAUDE.md` — drop GRE-specific "Pending work" section, generalize "Architecture" wording

---

## Pre-flight

- [ ] **Step 0a: Confirm starting branch is `main`**

```bash
git checkout main && git pull --ff-only
```

Expected: clean fast-forward.

- [ ] **Step 0b: Confirm baseline test counts**

```bash
dotnet build && dotnet test --no-build --verbosity minimal
```

Expected per-project counts:
- Architecture: **20**, Analyzers: **70**, Unit: **1**, Integration: **1**

If any number differs, **stop**.

---

## Task 1: Open issue + create feat branch

- [ ] **Step 1a: Create the M3 issue**

```bash
gh issue create --title "Restructure repo into dotnet new template layout" --body "Part of #13 (template extraction).

Move all existing content into content/, add .template.config/template.json (with sources mapping), add setup.ps1, READMEs, template-tests, and CI workflow. After this milestone, dotnet new install + scaffold produces a working project.

Plan: docs/superpowers/plans/2026-04-25-m3-template-machinery.md
Spec: docs/superpowers/specs/2026-04-25-etl-api-template-extraction-design.md"
```

Capture issue number as `<N>`.

- [ ] **Step 1b: Create the feat branch**

```bash
git checkout -b feat/<N>-template-machinery
```

---

## Task 2: The big move — everything into `content/`

This is the riskiest single step. Use `git mv` to preserve history. After the move, build verification confirms relative paths still resolve (Directory.Build.props uses `$(MSBuildThisFileDirectory)`, .slnx uses paths relative to itself).

- [ ] **Step 2a: Create `content/` directory and move files**

```bash
mkdir content
git mv src content/src
git mv tests content/tests
git mv .githooks content/.githooks
git mv .claude content/.claude
git mv .editorconfig content/.editorconfig
git mv .gitignore content/.gitignore
git mv Directory.Build.props content/Directory.Build.props
git mv GlobalRealEstate.slnx content/GlobalRealEstate.slnx
git mv CLAUDE.md content/CLAUDE.md
```

- [ ] **Step 2b: Verify nothing important was missed**

```bash
ls -A
```

Expected at root: `.git/`, `.vs/` (if present), `content/`, `docs/`. Nothing else (no stray src/tests/etc).

- [ ] **Step 2c: Build from new location**

```bash
dotnet build content/GlobalRealEstate.slnx
```

Expected: `Build succeeded`. If a path resolution fails, the relative paths in `Directory.Build.props` or the `.slnx` need adjustment.

- [ ] **Step 2d: Test from new location**

```bash
dotnet test content/GlobalRealEstate.slnx --no-build --verbosity minimal
```

Expected: same counts as pre-flight (20/70/1/1). Total **92 tests**.

- [ ] **Step 2e: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
refactor: move scaffolded content into content/ subdirectory

All existing repo files (src/, tests/, .githooks/, .claude/,
.editorconfig, .gitignore, Directory.Build.props, .slnx, CLAUDE.md)
moved into content/. The content/ directory holds everything that
gets scaffolded by 'dotnet new etl-api'; the repo root will hold
template-repo concerns (.template.config/, README.md, CI workflow,
template-tests/) added in subsequent commits.

After this commit the template-repo no longer has its own .githooks
firing on commits — that's intentional. Pre-commit hooks only matter
for scaffolded projects, where setup.ps1 will activate them.

Build + test still work via 'dotnet build content/GlobalRealEstate.slnx'.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

**Note:** the pre-commit hook still fires for THIS commit because git's `core.hooksPath` was set to `.githooks` and the move happens during the commit. Actually that's not quite right — git resolves `core.hooksPath` at commit time, after the working tree is staged. So:
- If `.githooks/` no longer exists at the configured path, hooks silently skip.
- After the move, `.githooks/` is at `content/.githooks/`, but `core.hooksPath` still points at `.githooks/`. Result: silent skip.

**That means the build/test verification in Steps 2c-2d is the LAST automated check before subsequent commits start landing without hook enforcement.** Don't skip those steps.

---

## Task 3: Add `.template.config/template.json`

- [ ] **Step 3a: Create the template manifest**

Write to `.template.config/template.json`:

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "ryan75195",
  "classifications": ["ETL", "API", "Console", "Web"],
  "identity": "EtlApi.Template",
  "name": "ETL + API solution",
  "shortName": "etl-api",
  "tags": {
    "language": "C#",
    "type": "solution"
  },
  "sourceName": "GlobalRealEstate",
  "preferNameDirectory": true,
  "sources": [
    {
      "source": "../content",
      "target": "./"
    }
  ],
  "primaryOutputs": [
    { "path": "src/GlobalRealEstate.Api/GlobalRealEstate.Api.csproj" },
    { "path": "src/GlobalRealEstate.Etl/GlobalRealEstate.Etl.csproj" }
  ],
  "postActions": [
    {
      "id": "restore",
      "actionId": "210D431B-A78B-4D2F-B762-4ED3E3EA9025",
      "continueOnError": true,
      "manualInstructions": [{ "text": "Run 'dotnet restore'" }]
    }
  ]
}
```

`sourceName` of `"GlobalRealEstate"` means `dotnet new etl-api -n MyApp` substitutes every literal `GlobalRealEstate` (in filenames, namespaces, file contents) with `MyApp`.

The `sources` array tells the template engine: "the actual templated files live at `../content/` relative to this manifest, and they should land at `./` of the output."

- [ ] **Step 3b: Commit**

```bash
git add .template.config/
git commit -m "$(cat <<'EOF'
feat: add dotnet new template manifest

.template.config/template.json declares the etl-api template:
- sourceName GlobalRealEstate gets substituted by 'dotnet new -n <Name>'
- sources mapping points template engine at content/
- primaryOutputs lets IDEs know the entry projects
- post-action triggers dotnet restore after scaffold

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Add `content/setup.ps1`

- [ ] **Step 4a: Create the post-scaffold script**

Write to `content/setup.ps1`:

```powershell
$ErrorActionPreference = 'Stop'

Write-Host "Initializing git repo..."
git init -q
git add .

Write-Host "Activating .githooks..."
git config core.hooksPath .githooks

Write-Host "Creating initial commit..."
git commit -q --no-verify -m "Initial scaffold from etl-api template"

Write-Host ""
Write-Host "Done. Next steps:"
Write-Host "  1. dotnet restore; dotnet build"
Write-Host "  2. gh repo create"
Write-Host "  3. gh issue create --title '...'"
```

`--no-verify` on the initial commit is deliberate — pre-commit would try to build an unrestored project.

- [ ] **Step 4b: Commit**

```bash
git add content/setup.ps1
git commit -m "$(cat <<'EOF'
feat: add setup.ps1 post-scaffold script

Activates .githooks via core.hooksPath, runs git init, and creates
the initial commit (with --no-verify since the project hasn't been
restored yet — every subsequent commit goes through the full hook
chain).

Scaffolded users run 'pwsh -File setup.ps1' (or '.\setup.ps1' on
Windows PowerShell) once after 'dotnet new etl-api -n MyApp'.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Add `content/README.md` (scaffolded project quickstart)

- [ ] **Step 5a: Create the scaffolded README**

Write to `content/README.md`:

```markdown
# GlobalRealEstate

ETL + API solution scaffolded from the [etl-api template](https://github.com/ryan75195/dotnet-etl-api-template).

## First-time setup

After scaffolding (`dotnet new etl-api -n GlobalRealEstate`), run once:

```powershell
.\setup.ps1
```

This initializes a git repo, activates `.githooks/` for the project lifecycle, and creates the initial commit.

## Build and test

```powershell
dotnet restore
dotnet build
dotnet test
```

## Development lifecycle

See [CLAUDE.md](./CLAUDE.md) for the full lifecycle (issue → branch → commit → PR).

Quick summary:
1. `gh issue create --title "..."` (every change starts with an issue)
2. `git checkout -b feat/<issue-num>-<slug>` (`reference-transaction` hook verifies the issue exists)
3. Edit + commit (pre-commit hook runs build, format, tests)
4. `gh pr create` and squash-merge

Direct commits to `main` are blocked. Edits to already-merged branches are blocked.
```

The literal `GlobalRealEstate` in the file gets substituted by `dotnet new -n <ProjectName>` to match the user's project name.

- [ ] **Step 5b: Commit**

```bash
git add content/README.md
git commit -m "$(cat <<'EOF'
feat: add README.md for scaffolded projects

Quickstart for newly-scaffolded projects. Walks through setup.ps1,
build/test, and points at CLAUDE.md for the full lifecycle.

The literal 'GlobalRealEstate' gets substituted by dotnet new -n <Name>.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Add root-level `README.md` (template usage docs)

- [ ] **Step 6a: Create the template README**

Write to `README.md` (at the repo root, NOT in content/):

```markdown
# dotnet-etl-api-template

A `dotnet new` template for .NET 10 ETL + API projects. Scaffolds:

- 4 source projects: `Core`, `Api`, `Etl`, `Analyzers`
- 4 test projects: `Tests.Unit`, `Tests.Integration`, `Tests.Architecture`, `Tests.Analyzers`
- 12 custom Roslyn analyzers (CI0001–CI0013) enforcing project-agnostic code rules
- 20 architecture tests (NetArchTest + reflection) split across 5 focused fixtures
- Three git/Claude hooks enforcing the development lifecycle
- A `CLAUDE.md` documenting the lifecycle for the scaffolded project

## Install

```powershell
dotnet new install https://github.com/ryan75195/dotnet-etl-api-template
```

(Or from a local clone: `dotnet new install <path-to-repo>`.)

## Use

```powershell
dotnet new etl-api -n MyDataPlatform
cd MyDataPlatform
.\setup.ps1
```

`MyDataPlatform` becomes the project name everywhere — namespaces, project files, the `.slnx`.

After `setup.ps1`:
- git repo is initialized
- `.githooks/` is active
- initial commit is in
- ready for `gh repo create` and the first issue

## Update

```powershell
dotnet new install https://github.com/ryan75195/dotnet-etl-api-template
```

(Same command; pulls the latest from `main`.)

## Uninstall

```powershell
dotnet new uninstall https://github.com/ryan75195/dotnet-etl-api-template
```

(Or whatever path you used at install time.)

## Repo layout

- `content/` — what gets scaffolded into the user's project
- `.template.config/template.json` — template manifest (sourceName, post-actions)
- `template-tests/` — local validation scripts
- `.github/workflows/template-ci.yml` — CI runs scaffold + build + test on every push

## Development

This template repo doesn't have its own pre-commit hooks (those only fire in scaffolded projects). Feature branches + PRs + the template-CI workflow are the safety net.

To verify changes locally before pushing:

```powershell
.\template-tests\scaffold-and-build.ps1
```
```

- [ ] **Step 6b: Commit**

```bash
git add README.md
git commit -m "$(cat <<'EOF'
docs: add root README for template usage

Documents how to install, use, update, and uninstall the etl-api
template. Also explains the repo layout and how to verify changes
locally during template development.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Add `template-tests/scaffold-and-build.ps1`

This is the local equivalent of what template-CI runs.

- [ ] **Step 7a: Create the validation script**

Write to `template-tests/scaffold-and-build.ps1`:

```powershell
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$scaffoldDir = Join-Path $env:TEMP "etl-api-template-smoke"

Write-Host "Cleaning previous smoke-test directory..."
if (Test-Path $scaffoldDir) {
    Remove-Item -Recurse -Force $scaffoldDir
}

Write-Host "Installing template from $repoRoot..."
dotnet new install $repoRoot --force

Write-Host "Scaffolding test project at $scaffoldDir..."
dotnet new etl-api -n SmokeTest -o $scaffoldDir

Write-Host "Building scaffolded project..."
Push-Location $scaffoldDir
try {
    dotnet build
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }

    Write-Host "Running scaffolded tests..."
    dotnet test --no-build --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Smoke test passed. Cleaning up..."
dotnet new uninstall $repoRoot
Remove-Item -Recurse -Force $scaffoldDir
Write-Host "Done."
```

- [ ] **Step 7b: Commit**

```bash
git add template-tests/scaffold-and-build.ps1
git commit -m "$(cat <<'EOF'
feat: add template-tests/scaffold-and-build.ps1

Local validation script that template-CI mirrors. Installs the
template from the current source, scaffolds a SmokeTest project,
builds, runs tests, then cleans up.

Use during template development to verify changes before pushing.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Add `.github/workflows/template-ci.yml`

- [ ] **Step 8a: Create the CI workflow**

Write to `.github/workflows/template-ci.yml`:

```yaml
name: template-ci
on:
  push:
    branches: [main]
  pull_request:

jobs:
  scaffold-and-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install template from current source
        run: dotnet new install ./

      - name: Scaffold test project
        run: dotnet new etl-api -n ScaffoldTest -o ${{ runner.temp }}/scaffold-test

      - name: Build scaffolded project
        working-directory: ${{ runner.temp }}/scaffold-test
        run: dotnet build --no-incremental

      - name: Run scaffolded tests
        working-directory: ${{ runner.temp }}/scaffold-test
        run: dotnet test --no-build --verbosity minimal
```

- [ ] **Step 8b: Commit**

```bash
git add .github/workflows/template-ci.yml
git commit -m "$(cat <<'EOF'
feat: add template-ci GitHub Actions workflow

Runs on every push to main and every PR. Installs the template from
current source, scaffolds a ScaffoldTest project, builds it, runs
tests. Catches regressions where a template change breaks the
scaffolded output.

Windows-only (matches Windows-first scope from the spec).

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: Update `content/CLAUDE.md` to drop GRE-specifics

The current `content/CLAUDE.md` has a "Pending work" section pointing at issue #8 (GRE-specific). The "Architecture" section names GRE projects but the projects are template-named (substituted at scaffold time), so that part stays as-is.

- [ ] **Step 9a: Read current CLAUDE.md to see what's there**

```bash
cat content/CLAUDE.md
```

- [ ] **Step 9b: Edit CLAUDE.md**

Two changes:

1. **Remove the "Pending work" section** (the entire `## Pending work` block including its body — references the GRE-specific issue #8).

2. **Fix the stale Architecture-tests path.** The current line:
   > `tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs` enforce layering...

   That file was split in M2. Replace with a reference to the directory:
   > `tests/GlobalRealEstate.Tests.Architecture/` enforce layering, DI shape, DI wiring (every public Core interface must be registered via `Core.ServiceCollectionExtensions.AddCoreServices()`), naming conventions, and one-public-type-per-file. Tests are split across LayerDependencyTests, NamingConventionTests, ServiceShapeTests, CodeStructureTests, and DiRegistrationTests; shared infrastructure lives in `TestHelpers.cs`.

The lifecycle, code style, key files sections all stay — they describe the scaffolded project's lifecycle, which is universal. The `GlobalRealEstate` literals get substituted at scaffold time.

- [ ] **Step 9c: Commit**

```bash
git add content/CLAUDE.md
git commit -m "$(cat <<'EOF'
docs: drop GRE-specific Pending work section from CLAUDE.md

The 'Pending work' section pointed at issue #8 in the original
GlobalRealEstate repo. Scaffolded projects don't inherit that issue
tracker, so removing.

Lifecycle, code style, architecture, and key files sections are
universal — keep them.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: Add root-level `.gitignore`

The original `.gitignore` moved into `content/` (it ignores bin/obj/.vs at scaffolded-project level). The template repo itself needs a small ignore for its own concerns.

- [ ] **Step 10a: Create root `.gitignore`**

Write to `.gitignore` (at the repo root):

```gitignore
# IDE state
.vs/
.idea/

# Smoke-test scratch directories
template-tests/scratch/

# OS junk
.DS_Store
Thumbs.db
```

- [ ] **Step 10b: Commit**

```bash
git add .gitignore
git commit -m "$(cat <<'EOF'
chore: add root .gitignore for template-repo concerns

The original .gitignore moved into content/ as part of M3's restructure.
This new root-level .gitignore covers template-repo-only concerns:
IDE state (.vs/, .idea/), smoke-test scratch, OS junk.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 11: Smoke-test the template locally

This is the moment of truth. Manually run `template-tests/scaffold-and-build.ps1` and confirm a scaffolded project actually builds and tests pass.

- [ ] **Step 11a: Run the smoke test**

```powershell
.\template-tests\scaffold-and-build.ps1
```

Expected output:
- "Installing template from ..."
- "Scaffolding test project at ..."
- "Building scaffolded project..." → Build succeeded
- "Running scaffolded tests..." → 92 tests passed (20 architecture + 70 analyzer + 1 unit smoke + 1 integration smoke)
- "Smoke test passed. Cleaning up..."
- "Done."

- [ ] **Step 11b: If smoke test fails — common issues**

If it fails:
- **`dotnet new install` fails** → check `.template.config/template.json` syntax (`sources` paths, identity field)
- **`dotnet new etl-api` fails** → check shortName matches (`etl-api`)
- **Build fails in scaffold** → likely a path issue in moved files; debug from the scaffolded directory
- **Tests fail in scaffold** → check that `SmokeTests.cs` files were correctly substituted (the namespace should be `ScaffoldTest.Tests.Unit`, not `GlobalRealEstate.Tests.Unit`)

Iterate fixes (each fix gets its own commit). Re-run smoke test until green.

- [ ] **Step 11c: No commit needed for the smoke test itself** (it doesn't modify the repo)

---

## Task 12: Push and open PR

- [ ] **Step 12a: Push**

```bash
git push -u origin HEAD
```

- [ ] **Step 12b: Open PR**

```bash
gh pr create --base main --title "Restructure repo into dotnet new template layout" --body "$(cat <<'EOF'
Closes #<N>. Part of #13 (template extraction).

## Changes

The repo restructures from 'a working solution' into 'a template that scaffolds a working solution':

- All existing files moved into \`content/\` (preserves history via git mv)
- \`.template.config/template.json\` at root — manifest with \`sources\` mapping to \`../content/\`
- \`content/setup.ps1\` — post-scaffold setup (git init, activate hooks, initial commit)
- \`content/README.md\` — scaffolded-project quickstart
- Root \`README.md\` — template usage docs
- \`template-tests/scaffold-and-build.ps1\` — local validation script
- \`.github/workflows/template-ci.yml\` — CI runs scaffold + build + test on every push (Windows-only)
- \`content/CLAUDE.md\` — dropped GRE-specific Pending work section
- Root \`.gitignore\` — covers template-repo-only concerns (IDE state, scratch, OS junk)

## Test plan

- [x] All 92 tests pass via \`dotnet test content/GlobalRealEstate.slnx\`
- [x] \`template-tests/scaffold-and-build.ps1\` succeeds end-to-end (install → scaffold → build → test → cleanup)
- [ ] Template-CI passes on this PR (will appear in checks once pushed)

After merge, M4 (rename repo to dotnet-etl-api-template + first install) is the next manual step.

Plan: \`docs/superpowers/plans/2026-04-25-m3-template-machinery.md\`
Spec: \`docs/superpowers/specs/2026-04-25-etl-api-template-extraction-design.md\`
EOF
)"
```

---

## Done

After Step 12b, M3 is complete. Reviewer eyeballs (the template-CI check is the critical signal) and merges. Next is M4 — repo rename + first install verification.
