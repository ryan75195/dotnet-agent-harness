# Agent Harness Plugin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the `agent-harness` Claude Code plugin (scaffolding skills + provenance stamp, git-gate feedback capture, `harness-update` skill) per `docs/superpowers/specs/2026-06-11-agent-harness-plugin-design.md`.

**Architecture:** A plugin under `plugin/` in this repo (repo doubles as its marketplace via `.claude-plugin/marketplace.json`). Scaffolding skills wrap the existing scaffold paths and write a `.harness.json` stamp. Capture lives in the templates' `.githooks/` (a shared `harness-feedback.sh` sourced by `pre-commit`/`post-commit`, appending JSONL events to `~/.agent-harness/feedback/`), with a plugin PostToolUse hook injecting the annotation instruction. `harness-update` classifies template changes (clean/modified/new) against per-template `harness-manifest.json` files via PowerShell scripts; the agent merges flagged files.

**Tech Stack:** PowerShell 5.1-compatible scripts, bash (Git for Windows) hooks, plain-PowerShell test scripts (repo convention — no Pester), GitHub Actions (windows-latest).

---

## Conventions for the executor (read first)

- **Working directory:** repo root `C:\Users\ryan7\programming\dotnet-agent-harness`. This repo commits directly to `main` (see git history) — no issue/branch lifecycle here.
- **New bash files (`harness-feedback.sh`, `post-commit`) MUST have LF line endings and no BOM.** The Write tool produces this by default — do not create them via PowerShell `Set-Content`.
- **`dotnet/templates/etl-api/.githooks/pre-commit` has CRLF line endings** (the cli one is LF). When editing it, Read it first and match `old_string` exactly. Task 6 normalizes it to LF.
- **PowerShell scripts must run on Windows PowerShell 5.1**: no `&&`, no ternary, no `?.`, no `Join-Path` with 3 args. Use `$(...)`/`if` and 2-arg `Join-Path` with `/` separators in child paths.
- All test scripts are plain PowerShell that `throw` on failure (matches `expo/template-tests/scaffold-and-validate.ps1` style).
- Run every command below from the repo root unless a `Push-Location`/`cd` is shown.

## File structure (what gets created/modified)

```
.claude-plugin/marketplace.json                          NEW  repo-as-marketplace
plugin/.claude-plugin/plugin.json                        NEW  plugin manifest
plugin/skills/new-dotnet-cli/SKILL.md                    NEW  scaffold skill
plugin/skills/new-dotnet-etl-api/SKILL.md                NEW  scaffold skill
plugin/skills/new-expo-app/SKILL.md                      NEW  scaffold skill
plugin/skills/harness-update/SKILL.md                    NEW  update skill
plugin/hooks/hooks.json                                  NEW  PostToolUse registration
plugin/hooks/feedback-annotate.ps1                       NEW  marker → additionalContext
plugin/scripts/resolve-repo.ps1                          NEW  local checkout / clone fallback
plugin/scripts/write-stamp.ps1                           NEW  writes .harness.json
plugin/scripts/harness-note.ps1                          NEW  appends note patch line
plugin/scripts/get-update.ps1                            NEW  classifies update candidates
plugin/scripts/apply-update.ps1                          NEW  applies clean/new files
plugin/template-tests/validate-plugin.ps1                NEW  manifest/skill sanity
plugin/template-tests/stamp-test.ps1                     NEW  write-stamp + resolve-repo tests
plugin/template-tests/update-scripts-test.ps1            NEW  get/apply-update tests
expo/templates/app/.githooks/harness-feedback.sh         NEW  shared capture lib
expo/templates/app/.githooks/post-commit                 NEW  fix linkage
expo/templates/app/.githooks/pre-commit                  MOD  run_gate + capture
expo/templates/app/harness-manifest.json                 NEW  owned paths
expo/templates/app/CLAUDE.md                             MOD  Harness maintenance section
dotnet/templates/cli/.githooks/harness-feedback.sh       NEW  copy (dotnet gates)
dotnet/templates/cli/.githooks/post-commit               NEW  copy
dotnet/templates/cli/.githooks/pre-commit                MOD  capture at 3 exit sites
dotnet/templates/cli/harness-manifest.json               NEW  owned paths
dotnet/templates/cli/CLAUDE.md                           MOD  Harness maintenance section
dotnet/templates/etl-api/(same four changes as cli)      NEW/MOD
expo/template-tests/scaffold-and-validate.ps1            MOD  end-to-end capture test
dotnet/template-tests/scaffold-and-build.ps1             MOD  capture unit test
.github/workflows/template-ci.yml                        MOD  plugin-validate job
README.md                                                MOD  plugin section
```

---

### Task 1: Plugin skeleton + validation script

**Files:**
- Create: `.claude-plugin/marketplace.json`
- Create: `plugin/.claude-plugin/plugin.json`
- Create: `plugin/template-tests/validate-plugin.ps1`

- [ ] **Step 1: Write the validation script (the test)**

Create `plugin/template-tests/validate-plugin.ps1`:

```powershell
param([switch]$RequireFull)

$ErrorActionPreference = 'Stop'

$pluginRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $pluginRoot

$pluginJsonPath = Join-Path $pluginRoot '.claude-plugin/plugin.json'
if (-not (Test-Path $pluginJsonPath)) { throw "Missing $pluginJsonPath" }
$pluginJson = Get-Content $pluginJsonPath -Raw | ConvertFrom-Json
if ($pluginJson.name -ne 'agent-harness') { throw "plugin.json name must be 'agent-harness', got '$($pluginJson.name)'" }
if (-not $pluginJson.description) { throw 'plugin.json missing description' }

$marketplacePath = Join-Path $repoRoot '.claude-plugin/marketplace.json'
if (-not (Test-Path $marketplacePath)) { throw "Missing $marketplacePath" }
$marketplace = Get-Content $marketplacePath -Raw | ConvertFrom-Json
$entry = $marketplace.plugins | Where-Object { $_.name -eq 'agent-harness' }
if (-not $entry) { throw 'marketplace.json must list the agent-harness plugin' }
if ($entry.source -ne './plugin') { throw "marketplace entry source must be './plugin', got '$($entry.source)'" }

$skillsDir = Join-Path $pluginRoot 'skills'
$skillDirs = @()
if (Test-Path $skillsDir) { $skillDirs = @(Get-ChildItem $skillsDir -Directory) }
foreach ($dir in $skillDirs) {
    $skillPath = Join-Path $dir.FullName 'SKILL.md'
    if (-not (Test-Path $skillPath)) { throw "Missing SKILL.md in $($dir.FullName)" }
    $text = Get-Content $skillPath -Raw
    if ($text -notmatch ('(?s)^---.*?name:\s*' + [regex]::Escape($dir.Name) + '\b.*?---')) {
        throw "$($dir.Name)/SKILL.md frontmatter must declare 'name: $($dir.Name)'"
    }
    if ($text -notmatch 'description:\s*\S') { throw "$($dir.Name)/SKILL.md frontmatter missing a description" }
}

if ($RequireFull) {
    $required = @('new-dotnet-cli', 'new-dotnet-etl-api', 'new-expo-app', 'harness-update')
    foreach ($name in $required) {
        if (-not (Test-Path (Join-Path $skillsDir "$name/SKILL.md"))) { throw "Required skill missing: $name" }
    }
    $hooksPath = Join-Path $pluginRoot 'hooks/hooks.json'
    if (-not (Test-Path $hooksPath)) { throw "Missing $hooksPath" }
    $hooks = Get-Content $hooksPath -Raw | ConvertFrom-Json
    if (-not $hooks.hooks.PostToolUse) { throw 'hooks.json must register a PostToolUse hook' }
    foreach ($script in @('resolve-repo.ps1', 'write-stamp.ps1', 'harness-note.ps1', 'get-update.ps1', 'apply-update.ps1')) {
        if (-not (Test-Path (Join-Path $pluginRoot "scripts/$script"))) { throw "Required script missing: $script" }
    }
}

Write-Host 'Plugin validation passed.'
```

- [ ] **Step 2: Run it to verify it fails**

Run: `./plugin/template-tests/validate-plugin.ps1`
Expected: FAIL with `Missing ...plugin\.claude-plugin\plugin.json`

- [ ] **Step 3: Create the manifests**

Create `plugin/.claude-plugin/plugin.json`:

```json
{
  "name": "agent-harness",
  "description": "Scaffold, update, and capture feedback from agent-harness template projects",
  "version": "0.1.0",
  "author": { "name": "ryan75195" }
}
```

Create `.claude-plugin/marketplace.json`:

```json
{
  "name": "agent-harness",
  "owner": { "name": "ryan75195" },
  "plugins": [
    {
      "name": "agent-harness",
      "source": "./plugin",
      "description": "Scaffold, update, and capture feedback from agent-harness template projects"
    }
  ]
}
```

- [ ] **Step 4: Run validation to verify it passes**

Run: `./plugin/template-tests/validate-plugin.ps1`
Expected: `Plugin validation passed.`

- [ ] **Step 5: Commit**

```powershell
git add .claude-plugin plugin
git commit -m "Add agent-harness plugin skeleton and validation script"
```

---

### Task 2: write-stamp.ps1

**Files:**
- Create: `plugin/scripts/write-stamp.ps1`
- Create: `plugin/template-tests/stamp-test.ps1`

- [ ] **Step 1: Write the failing test**

Create `plugin/template-tests/stamp-test.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
if ($null -ne (Get-Variable -Name PSNativeCommandUseErrorActionPreference -Scope Global -ErrorAction SilentlyContinue)) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$pluginScripts = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts'
$work = Join-Path $env:TEMP 'agent-harness-stamp-test'
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory $work | Out-Null

function Assert([bool]$Cond, [string]$Msg) { if (-not $Cond) { throw "ASSERT FAILED: $Msg" } }

$repo = Join-Path $work 'repo'
New-Item -ItemType Directory $repo | Out-Null
git -C $repo init -q -b main
git -C $repo config user.email t@t.local
git -C $repo config user.name t
New-Item -ItemType Directory (Join-Path $repo 'dotnet/templates/cli/.template.config') -Force | Out-Null
Set-Content (Join-Path $repo 'dotnet/templates/cli/.template.config/template.json') '{ "sourceName": "ConsoleApp" }'
git -C $repo add .
git -C $repo commit -qm init
$sha = (git -C $repo rev-parse HEAD).Trim()

$proj = Join-Path $work 'proj'
New-Item -ItemType Directory $proj | Out-Null
& (Join-Path $pluginScripts 'write-stamp.ps1') -ProjectDir $proj -Template cli -ProjectName MyTool -RepoPath $repo
$stamp = Get-Content (Join-Path $proj '.harness.json') -Raw | ConvertFrom-Json
Assert ($stamp.template -eq 'cli') 'template field'
Assert ($stamp.stack -eq 'dotnet') 'stack field'
Assert ($stamp.templateDir -eq 'dotnet/templates/cli') 'templateDir field'
Assert ($stamp.scaffoldCommit -eq $sha) 'scaffoldCommit field'
Assert ($stamp.lastUpdateCommit -eq $sha) 'lastUpdateCommit field'
Assert ($stamp.renames.ConsoleApp -eq 'MyTool') 'dotnet sourceName rename'

$proj2 = Join-Path $work 'proj2'
New-Item -ItemType Directory $proj2 | Out-Null
& (Join-Path $pluginScripts 'write-stamp.ps1') -ProjectDir $proj2 -Template expo-app -ProjectName MyShinyApp -RepoPath $repo -BundleId com.example.myshinyapp
$stamp2 = Get-Content (Join-Path $proj2 '.harness.json') -Raw | ConvertFrom-Json
Assert ($stamp2.stack -eq 'expo') 'expo stack'
Assert ($stamp2.templateDir -eq 'expo/templates/app') 'expo templateDir'
Assert ($stamp2.renames.'com.example.apptemplate' -eq 'com.example.myshinyapp') 'bundle id rename'
Assert ($stamp2.renames.'AppTemplate' -eq 'MyShinyApp') 'name rename'
Assert ($stamp2.renames.'app-template' -eq 'my-shiny-app') 'slug rename'
Assert ($stamp2.renames.'apptemplate' -eq 'myshinyapp') 'lowercase rename'

$proj3 = Join-Path $work 'proj3'
New-Item -ItemType Directory $proj3 | Out-Null
& (Join-Path $pluginScripts 'write-stamp.ps1') -ProjectDir $proj3 -Template expo-app -ProjectName Plain -RepoPath $repo
$stamp3 = Get-Content (Join-Path $proj3 '.harness.json') -Raw | ConvertFrom-Json
Assert ($stamp3.renames.'com.example.apptemplate' -eq 'com.example.plain') 'default bundle id'

Write-Host 'Stamp tests passed.'
Remove-Item -Recurse -Force $work
```

- [ ] **Step 2: Run test to verify it fails**

Run: `./plugin/template-tests/stamp-test.ps1`
Expected: FAIL — `write-stamp.ps1` does not exist (`The term ... is not recognized` / path not found).

- [ ] **Step 3: Write write-stamp.ps1**

Create `plugin/scripts/write-stamp.ps1`. The rename order matters: `com.example.apptemplate` must be replaced before `apptemplate` (it contains it) — this mirrors `expo/new-app.ps1` lines 30–34. The slug derivation (`-creplace '(?<=[a-z0-9])(?=[A-Z])', '-'`) is copied from `new-app.ps1` line 23.

```powershell
param(
    [Parameter(Mandatory = $true)][string]$ProjectDir,
    [Parameter(Mandatory = $true)][ValidateSet('cli', 'etl-api', 'expo-app')][string]$Template,
    [Parameter(Mandatory = $true)][string]$ProjectName,
    [Parameter(Mandatory = $true)][string]$RepoPath,
    [string]$BundleId
)

$ErrorActionPreference = 'Stop'

$templateDirs = @{
    'cli'      = 'dotnet/templates/cli'
    'etl-api'  = 'dotnet/templates/etl-api'
    'expo-app' = 'expo/templates/app'
}
$templateDir = $templateDirs[$Template]

$commit = (git -C $RepoPath rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw "Could not resolve HEAD of harness repo at $RepoPath" }

if ($Template -eq 'expo-app') {
    if (-not $BundleId) { $BundleId = "com.example.$($ProjectName.ToLower())" }
    $slug = ($ProjectName -creplace '(?<=[a-z0-9])(?=[A-Z])', '-').ToLower()
    $renames = [ordered]@{
        'com.example.apptemplate' = $BundleId
        'AppTemplate'             = $ProjectName
        'app-template'            = $slug
        'apptemplate'             = $ProjectName.ToLower()
    }
    $stack = 'expo'
}
else {
    $templateJsonPath = Join-Path $RepoPath "$templateDir/.template.config/template.json"
    $sourceName = (Get-Content $templateJsonPath -Raw | ConvertFrom-Json).sourceName
    $renames = [ordered]@{ $sourceName = $ProjectName }
    $stack = 'dotnet'
}

$stamp = [ordered]@{
    template         = $Template
    stack            = $stack
    repoUrl          = 'https://github.com/ryan75195/dotnet-agent-harness'
    templateDir      = $templateDir
    scaffoldCommit   = $commit
    lastUpdateCommit = $commit
    renames          = $renames
    scaffoldedAt     = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
}

$json = $stamp | ConvertTo-Json -Depth 5
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[IO.File]::WriteAllText((Join-Path $ProjectDir '.harness.json'), $json, $utf8NoBom)
Write-Output "Wrote .harness.json (template=$Template, commit=$commit)"
```

- [ ] **Step 4: Run test to verify it passes**

Run: `./plugin/template-tests/stamp-test.ps1`
Expected: `Stamp tests passed.`

- [ ] **Step 5: Commit**

```powershell
git add plugin/scripts/write-stamp.ps1 plugin/template-tests/stamp-test.ps1
git commit -m "Add write-stamp script producing .harness.json provenance"
```

---

### Task 3: resolve-repo.ps1

**Files:**
- Create: `plugin/scripts/resolve-repo.ps1`
- Modify: `plugin/template-tests/stamp-test.ps1` (append resolve-repo tests)

- [ ] **Step 1: Append the failing test**

In `plugin/template-tests/stamp-test.ps1`, insert before the final `Write-Host 'Stamp tests passed.'` line:

```powershell
$cfg = Join-Path $work 'config.json'
$escapedRepo = $repo -replace '\\', '\\'
Set-Content $cfg ('{ "repoPath": "' + $escapedRepo + '" }')
$resolved = & (Join-Path $pluginScripts 'resolve-repo.ps1') -ConfigPath $cfg | Select-Object -Last 1
Assert ($resolved -eq $repo) "resolve-repo should return configured checkout, got '$resolved'"

$cfgMissing = Join-Path $work 'config-missing.json'
Set-Content $cfgMissing '{ "repoPath": "C:/does/not/exist" }'
$failed = $false
try {
    & (Join-Path $pluginScripts 'resolve-repo.ps1') -ConfigPath $cfgMissing -NoClone | Out-Null
}
catch { $failed = $true }
Assert $failed 'resolve-repo with bad config and -NoClone must throw'
```

Note: `-replace '\\', '\\'` doubles backslashes — the replacement string `'\\'` is a regex substitution producing two literal backslashes, valid JSON escaping.

- [ ] **Step 2: Run test to verify it fails**

Run: `./plugin/template-tests/stamp-test.ps1`
Expected: FAIL at the resolve-repo line (script not found).

- [ ] **Step 3: Write resolve-repo.ps1**

Create `plugin/scripts/resolve-repo.ps1`. `-NoClone` exists so tests never touch the network; the last line of stdout is the resolved path (callers use `Select-Object -Last 1`).

```powershell
param(
    [string]$RepoUrl = 'https://github.com/ryan75195/dotnet-agent-harness',
    [string]$ConfigPath = (Join-Path $HOME '.agent-harness/config.json'),
    [switch]$NoClone
)

$ErrorActionPreference = 'Stop'

if (Test-Path $ConfigPath) {
    $cfg = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    if ($cfg.repoPath) {
        if (Test-Path (Join-Path $cfg.repoPath '.git')) {
            git -C $cfg.repoPath fetch --quiet origin main
            if ($LASTEXITCODE -ne 0) {
                Write-Warning 'fetch failed (offline or no origin) - using checkout as-is'
                $global:LASTEXITCODE = 0
            }
            Write-Output $cfg.repoPath
            exit 0
        }
        throw "config.json repoPath '$($cfg.repoPath)' is not a git checkout"
    }
}

if ($NoClone) { throw "No usable local checkout configured at $ConfigPath and cloning is disabled" }

$clonePath = Join-Path $env:TEMP 'agent-harness-repo'
if (Test-Path (Join-Path $clonePath '.git')) {
    git -C $clonePath fetch --quiet origin main
    if ($LASTEXITCODE -eq 0) {
        git -C $clonePath merge --ff-only --quiet origin/main | Out-Null
    }
    else {
        Write-Warning 'fetch failed (offline?) - using cached clone as-is'
        $global:LASTEXITCODE = 0
    }
}
else {
    if (Test-Path $clonePath) { Remove-Item -Recurse -Force $clonePath }
    git clone --quiet $RepoUrl $clonePath
    if ($LASTEXITCODE -ne 0) { throw "Failed to clone $RepoUrl. Configure a local checkout in $ConfigPath as { `"repoPath`": `"...`" }" }
}
Write-Output $clonePath
```

- [ ] **Step 4: Run test to verify it passes**

Run: `./plugin/template-tests/stamp-test.ps1`
Expected: `Stamp tests passed.` (the bad-config case throws and is caught).

- [ ] **Step 5: Commit**

```powershell
git add plugin/scripts/resolve-repo.ps1 plugin/template-tests/stamp-test.ps1
git commit -m "Add resolve-repo script with local checkout and clone fallback"
```

---

### Task 4: harness-note.ps1 + plugin annotation hook

**Files:**
- Create: `plugin/scripts/harness-note.ps1`
- Create: `plugin/hooks/feedback-annotate.ps1`
- Create: `plugin/hooks/hooks.json`
- Create: `plugin/template-tests/annotate-test.ps1`

- [ ] **Step 1: Write the failing test**

Create `plugin/template-tests/annotate-test.ps1`:

```powershell
$ErrorActionPreference = 'Stop'

$pluginRoot = Split-Path -Parent $PSScriptRoot
$work = Join-Path $env:TEMP 'agent-harness-annotate-test'
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory (Join-Path $work 'feedback') -Force | Out-Null

function Assert([bool]$Cond, [string]$Msg) { if (-not $Cond) { throw "ASSERT FAILED: $Msg" } }

$payload = '{"tool_name":"Bash","tool_response":{"stdout":"LINT FAILED\nHARNESS-FEEDBACK: event ab12cd logged - append a one-line note on what this code was trying to do."}}'
$result = $payload | powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $pluginRoot 'hooks/feedback-annotate.ps1') | Out-String
Assert ($result -match 'ab12cd') 'annotate hook must surface the event id'
Assert ($result -match 'additionalContext') 'annotate hook must emit additionalContext JSON'
Assert ($result -match 'harness-note.ps1') 'annotate hook must point at harness-note.ps1'

$quiet = '{"tool_name":"Bash","tool_response":{"stdout":"all good"}}' | powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $pluginRoot 'hooks/feedback-annotate.ps1') | Out-String
Assert (-not ($quiet -match 'additionalContext')) 'no marker means no output'

$env:AGENT_HARNESS_FEEDBACK_DIR = Join-Path $work 'feedback'
Set-Content (Join-Path $work 'feedback/events.jsonl') '{"id":"ab12cd","gate":"lint"}'
& (Join-Path $pluginRoot 'scripts/harness-note.ps1') -EventId ab12cd -Note 'adding the paywall screen'
$store = Get-Content (Join-Path $work 'feedback/events.jsonl') -Raw
Assert ($store -match '"note":\s*"adding the paywall screen"') 'note patch line appended'
Remove-Item Env:AGENT_HARNESS_FEEDBACK_DIR

Write-Host 'Annotate tests passed.'
Remove-Item -Recurse -Force $work
```

- [ ] **Step 2: Run test to verify it fails**

Run: `./plugin/template-tests/annotate-test.ps1`
Expected: FAIL — `feedback-annotate.ps1` not found.

- [ ] **Step 3: Write the three files**

Create `plugin/hooks/feedback-annotate.ps1`:

```powershell
$ErrorActionPreference = 'SilentlyContinue'

$payload = [Console]::In.ReadToEnd()
if ($payload -match 'HARNESS-FEEDBACK: event ([0-9a-f]+)') {
    $eventId = $Matches[1]
    $notePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../scripts/harness-note.ps1'))
    $context = "A harness feedback event '$eventId' was just logged because a guardrail blocked this commit. " +
        "Before fixing the failure, append a one-line note describing what the failing code was trying to do: " +
        "powershell -NoProfile -File `"$notePath`" -EventId $eventId -Note `"<one-line description>`" " +
        "Then fix the failure and commit again."
    @{ hookSpecificOutput = @{ hookEventName = 'PostToolUse'; additionalContext = $context } } | ConvertTo-Json -Compress
}
exit 0
```

Create `plugin/scripts/harness-note.ps1`:

```powershell
param(
    [Parameter(Mandatory = $true)][string]$EventId,
    [Parameter(Mandatory = $true)][string]$Note
)

$ErrorActionPreference = 'Stop'

$feedbackDir = if ($env:AGENT_HARNESS_FEEDBACK_DIR) { $env:AGENT_HARNESS_FEEDBACK_DIR } else { Join-Path $HOME '.agent-harness/feedback' }
$store = Join-Path $feedbackDir 'events.jsonl'
if (-not (Test-Path $store)) { throw "Feedback store not found: $store" }

$line = [pscustomobject]@{ id = $EventId; note = $Note } | ConvertTo-Json -Compress
Add-Content -Path $store -Value $line
Write-Output "Note appended to event $EventId."
```

Create `plugin/hooks/hooks.json`:

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Bash|PowerShell",
        "hooks": [
          {
            "type": "command",
            "command": "powershell -NoProfile -ExecutionPolicy Bypass -File \"${CLAUDE_PLUGIN_ROOT}/hooks/feedback-annotate.ps1\""
          }
        ]
      }
    ]
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `./plugin/template-tests/annotate-test.ps1`
Expected: `Annotate tests passed.`

- [ ] **Step 5: Commit**

```powershell
git add plugin/hooks plugin/scripts/harness-note.ps1 plugin/template-tests/annotate-test.ps1
git commit -m "Add feedback annotation hook and harness-note script"
```

---

### Task 5: Expo template capture (harness-feedback.sh, pre-commit, post-commit, template test)

**Files:**
- Create: `expo/templates/app/.githooks/harness-feedback.sh` (LF, via Write tool)
- Create: `expo/templates/app/.githooks/post-commit` (LF, via Write tool)
- Modify: `expo/templates/app/.githooks/pre-commit`
- Modify: `expo/template-tests/scaffold-and-validate.ps1`

- [ ] **Step 1: Add the end-to-end capture test (failing first)**

In `expo/template-tests/scaffold-and-validate.ps1`, insert immediately before the closing `}` of the `try` block (after the account-deletion doctor check, line ~87):

```powershell
    Write-Host "Feedback capture: blocked commit must log an event..."
    $feedbackDir = Join-Path $env:TEMP 'agent-harness-feedback-test'
    if (Test-Path $feedbackDir) { Remove-Item -Recurse -Force $feedbackDir }
    $env:AGENT_HARNESS_FEEDBACK_DIR = ($feedbackDir -replace '\\', '/')
    git init -q -b main
    git config user.email smoke@test.local
    git config user.name smoke
    git checkout -q -b capture-test
    git config core.hooksPath .githooks
    Set-Content -Path '.harness.json' -Value '{ "template": "expo-app", "stack": "expo" }'
    Set-Content -Path 'src\lib\seeded.ts' -Value "// seeded violation`nexport const seeded = 1;"
    git add .
    $commitOut = git commit -m "capture probe" 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) { throw 'commit unexpectedly succeeded with a seeded lint violation' }
    if ($commitOut -notmatch 'HARNESS-FEEDBACK: event ([0-9a-f]+)') { throw "no HARNESS-FEEDBACK marker in commit output:`n$commitOut" }
    $eventId = $Matches[1]
    $events = Get-Content (Join-Path $feedbackDir 'events.jsonl') -Raw
    if ($events -notmatch ('"id":"' + $eventId + '"')) { throw 'event id not found in events.jsonl' }
    if ($events -notmatch '"gate":"lint"') { throw "event gate is not lint:`n$events" }
    if (-not (Test-Path (Join-Path $feedbackDir "diffs/$eventId.failure.patch"))) { throw 'failure patch missing' }

    Write-Host "Feedback capture: next passing commit must link the fix..."
    git rm -q --cached src/lib/seeded.ts
    Remove-Item 'src\lib\seeded.ts'
    git commit -q -m "capture probe fixed"
    if ($LASTEXITCODE -ne 0) { throw 'fix commit failed' }
    $events = Get-Content (Join-Path $feedbackDir 'events.jsonl') -Raw
    if ($events -notmatch ('"id":"' + $eventId + '","fixCommit":"[0-9a-f]{40}"')) { throw 'fixCommit patch line missing' }
    if (-not (Test-Path (Join-Path $feedbackDir "diffs/$eventId.fix.patch"))) { throw 'fix patch missing' }
    if (Test-Path '.git\harness-pending-event') { throw 'pending-event marker not cleaned up' }
    Remove-Item Env:AGENT_HARNESS_FEEDBACK_DIR
    Remove-Item -Recurse -Force $feedbackDir
```

Notes for the executor:
- The env var uses forward slashes so the bash hook can `mkdir -p` it.
- Branch `capture-test` (not `feat/*`) avoids `block-merged-branch.sh` (it only gates `feat/*`) and is created before `core.hooksPath` is set so `reference-transaction` never fires.
- The probe commit fails at the **lint** gate (typecheck passes a `//` comment; eslint `local/no-comments` does not).

- [ ] **Step 2: Run the template test to verify the new section fails**

Run: `./expo/template-tests/scaffold-and-validate.ps1`
Expected: FAIL with `no HARNESS-FEEDBACK marker in commit output` (the hook doesn't log yet). This run takes several minutes (npm install + full gate twice).

- [ ] **Step 3: Create harness-feedback.sh**

Create `expo/templates/app/.githooks/harness-feedback.sh` **with the Write tool** (LF endings):

```bash
#!/bin/bash

HARNESS_FEEDBACK_DIR="${AGENT_HARNESS_FEEDBACK_DIR:-$HOME/.agent-harness/feedback}"

harness_stamp_field() {
  local root field
  field="$1"
  root=$(git rev-parse --show-toplevel 2>/dev/null) || return 1
  [ -f "$root/.harness.json" ] || return 1
  sed -n 's/.*"'"$field"'"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$root/.harness.json" | head -1
}

harness_json_escape() {
  printf '%s' "$1" | tr -d '\r' | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g' -e 's/\t/\\t/g' | awk '{printf "%s\\n", $0}' | sed 's/\\n$//'
}

harness_log_failure() {
  (
    set +e
    local gate output root id ts branch template tail_text escaped project gitdir
    gate="$1"
    output="$2"
    root=$(git rev-parse --show-toplevel 2>/dev/null) || exit 0
    [ -f "$root/.harness.json" ] || exit 0
    mkdir -p "$HARNESS_FEEDBACK_DIR/diffs" 2>/dev/null || exit 0
    id=$(openssl rand -hex 3 2>/dev/null)
    [ -n "$id" ] || id=$(printf '%06x' $(( $(date +%s) % 16777216 )))
    ts=$(date -u +%Y-%m-%dT%H:%M:%SZ)
    branch=$(git branch --show-current 2>/dev/null)
    template=$(harness_stamp_field template)
    tail_text=$(printf '%s' "$output" | tail -n 50)
    escaped=$(harness_json_escape "$tail_text")
    project=$(harness_json_escape "$root")
    printf '{"id":"%s","ts":"%s","project":"%s","template":"%s","gate":"%s","branch":"%s","outputTail":"%s","note":null,"fixCommit":null}\n' \
      "$id" "$ts" "$project" "$template" "$gate" "$branch" "$escaped" \
      >> "$HARNESS_FEEDBACK_DIR/events.jsonl" 2>/dev/null || exit 0
    git diff --cached > "$HARNESS_FEEDBACK_DIR/diffs/$id.failure.patch" 2>/dev/null
    gitdir=$(git rev-parse --git-dir 2>/dev/null)
    [ -n "$gitdir" ] && echo "$id" > "$gitdir/harness-pending-event" 2>/dev/null
    echo "HARNESS-FEEDBACK: event $id logged - append a one-line note on what this code was trying to do."
  )
  return 0
}
```

Everything inside the subshell is fail-open: any error exits the subshell with 0 and the gate's own exit code is what the commit sees.

- [ ] **Step 4: Create post-commit**

Create `expo/templates/app/.githooks/post-commit` **with the Write tool** (LF endings):

```bash
#!/bin/bash

REPO_ROOT=$(git rev-parse --show-toplevel 2>/dev/null) || exit 0
[ -f "$REPO_ROOT/.githooks/harness-feedback.sh" ] || exit 0
. "$REPO_ROOT/.githooks/harness-feedback.sh"

GIT_DIR=$(git rev-parse --git-dir 2>/dev/null) || exit 0
PENDING="$GIT_DIR/harness-pending-event"
[ -f "$PENDING" ] || exit 0
(
  set +e
  ID=$(cat "$PENDING")
  SHA=$(git rev-parse HEAD 2>/dev/null)
  if [ -n "$ID" ] && [ -n "$SHA" ]; then
    printf '{"id":"%s","fixCommit":"%s"}\n' "$ID" "$SHA" >> "$HARNESS_FEEDBACK_DIR/events.jsonl" 2>/dev/null
    git show HEAD --format= --patch > "$HARNESS_FEEDBACK_DIR/diffs/$ID.fix.patch" 2>/dev/null
  fi
  rm -f "$PENDING" 2>/dev/null
)
exit 0
```

- [ ] **Step 5: Rewire pre-commit through run_gate**

In `expo/templates/app/.githooks/pre-commit`, replace everything from `echo "=== Pre-Commit Checks ==="` through the jest line (lines 16–31 of the current file) with:

```bash
if [ -f "$REPO_ROOT/.githooks/harness-feedback.sh" ]; then
  . "$REPO_ROOT/.githooks/harness-feedback.sh"
else
  harness_log_failure() { :; }
fi

run_gate() {
  local gate_name
  gate_name="$1"; shift
  GATE_OUTPUT=$("$@" 2>&1)
  GATE_EXIT=$?
  printf '%s\n' "$GATE_OUTPUT"
  if [ $GATE_EXIT -ne 0 ]; then
    harness_log_failure "$gate_name" "$GATE_OUTPUT"
    return 1
  fi
  return 0
}

echo "=== Pre-Commit Checks ==="

echo "--- Typecheck ---"
run_gate typecheck npx tsc --noEmit || { echo "TYPECHECK FAILED. Fix all type errors before committing."; exit 1; }

echo "--- Lint ---"
run_gate lint npx eslint . --max-warnings 0 || { echo "LINT FAILED. local/no-comments and friends fire at error severity."; exit 1; }

echo "--- Dependency rules ---"
run_gate depcruise npx depcruise src --config .dependency-cruiser.cjs || { echo "DEPENDENCY RULES FAILED. See .dependency-cruiser.cjs."; exit 1; }

echo "--- Test files ---"
run_gate test-files node scripts/check-test-files.js || exit 1

echo "--- Tests + coverage ---"
run_gate tests npx jest --coverage --silent || { echo "TESTS FAILED (or coverage below threshold)."; exit 1; }
```

The trailing `echo "=== All checks passed ==="` / `exit 0` lines stay unchanged.

- [ ] **Step 6: Run the template test to verify it passes**

Run: `./expo/template-tests/scaffold-and-validate.ps1`
Expected: ends with `Expo template validation passed.` including both new capture sections. (Several minutes.)

- [ ] **Step 7: Commit**

```powershell
git add expo/templates/app/.githooks expo/template-tests/scaffold-and-validate.ps1
git commit -m "Capture guardrail failures from the expo template git gate"
```

---

### Task 6: .NET template capture (cli + etl-api)

**Files:**
- Create: `dotnet/templates/cli/.githooks/harness-feedback.sh` (identical content to Task 5 Step 3 — copy the file)
- Create: `dotnet/templates/cli/.githooks/post-commit` (identical content to Task 5 Step 4)
- Modify: `dotnet/templates/cli/.githooks/pre-commit`
- Same three changes in `dotnet/templates/etl-api/.githooks/` (plus LF normalization of its pre-commit)
- Modify: `dotnet/template-tests/scaffold-and-build.ps1`

- [ ] **Step 1: Add the failing capture test**

In `dotnet/template-tests/scaffold-and-build.ps1`, insert before the `finally` line (after the `dotnet test` block, line ~32):

```powershell
    Write-Host "Feedback capture: harness_log_failure must append an event..."
    $feedbackDir = Join-Path $env:TEMP 'agent-harness-feedback-test-dotnet'
    if (Test-Path $feedbackDir) { Remove-Item -Recurse -Force $feedbackDir }
    $env:AGENT_HARNESS_FEEDBACK_DIR = ($feedbackDir -replace '\\', '/')
    git init -q -b main
    git config user.email smoke@test.local
    git config user.name smoke
    Set-Content '.harness.json' ('{ "template": "' + $Template + '", "stack": "dotnet" }')
    $markerOut = bash -c '. .githooks/harness-feedback.sh; harness_log_failure build "error CS0000: seeded failure"' | Out-String
    if ($markerOut -notmatch 'HARNESS-FEEDBACK: event ([0-9a-f]+)') { throw "marker not emitted: $markerOut" }
    $eventId = $Matches[1]
    $events = Get-Content (Join-Path $feedbackDir 'events.jsonl') -Raw
    if ($events -notmatch '"gate":"build"') { throw "build event not logged: $events" }
    if ($events -notmatch 'seeded failure') { throw 'outputTail missing from event' }

    Write-Host "Feedback capture: post-commit must link the fix..."
    git add .harness.json
    git commit -q --no-verify -m "probe"
    bash .githooks/post-commit
    $events = Get-Content (Join-Path $feedbackDir 'events.jsonl') -Raw
    if ($events -notmatch ('"id":"' + $eventId + '","fixCommit":"[0-9a-f]{40}"')) { throw 'fixCommit patch line missing' }
    Remove-Item Env:AGENT_HARNESS_FEEDBACK_DIR
    Remove-Item -Recurse -Force $feedbackDir
```

- [ ] **Step 2: Run to verify it fails**

Run: `./dotnet/template-tests/scaffold-and-build.ps1 -Template cli`
Expected: FAIL with `marker not emitted` (harness-feedback.sh missing in scaffold).

- [ ] **Step 3: Copy the capture files into both dotnet templates**

```powershell
Copy-Item expo/templates/app/.githooks/harness-feedback.sh dotnet/templates/cli/.githooks/harness-feedback.sh
Copy-Item expo/templates/app/.githooks/post-commit dotnet/templates/cli/.githooks/post-commit
Copy-Item expo/templates/app/.githooks/harness-feedback.sh dotnet/templates/etl-api/.githooks/harness-feedback.sh
Copy-Item expo/templates/app/.githooks/post-commit dotnet/templates/etl-api/.githooks/post-commit
```

- [ ] **Step 4: Wire capture into cli pre-commit**

`dotnet/templates/cli/.githooks/pre-commit` — four edits:

(a) After the block-merged-branch check (after line 17, `fi`), insert:

```bash
if [ -f "$REPO_ROOT/.githooks/harness-feedback.sh" ]; then
  . "$REPO_ROOT/.githooks/harness-feedback.sh"
else
  harness_log_failure() { :; }
fi
```

(b) Build failure — old:

```bash
  echo "Fix all errors before committing."
  exit 1
```

new:

```bash
  echo "Fix all errors before committing."
  harness_log_failure build "$BUILD_OUTPUT"
  exit 1
```

(c) Lint failure — old:

```bash
  echo "LINT FAILED. Run 'dotnet format' to fix:"
  echo "$FORMAT_OUTPUT" | head -20
  exit 1
```

new:

```bash
  echo "LINT FAILED. Run 'dotnet format' to fix:"
  echo "$FORMAT_OUTPUT" | head -20
  harness_log_failure lint "$FORMAT_OUTPUT"
  exit 1
```

(d) Test failures — accumulate output in the loop. Old:

```bash
    echo ""
    TESTS_FAILED=1
```

new:

```bash
    echo ""
    TESTS_FAILED=1
    FAILED_TEST_OUTPUT="$FAILED_TEST_OUTPUT$PROJ_NAME:
$TEST_OUTPUT
"
```

and old:

```bash
if [ "$TESTS_FAILED" -eq 1 ]; then
  echo "TESTS FAILED. Fix failing tests before committing."
  exit 1
fi
```

new:

```bash
if [ "$TESTS_FAILED" -eq 1 ]; then
  echo "TESTS FAILED. Fix failing tests before committing."
  harness_log_failure tests "$FAILED_TEST_OUTPUT"
  exit 1
fi
```

- [ ] **Step 5: Same edits in etl-api pre-commit, normalized to LF**

`dotnet/templates/etl-api/.githooks/pre-commit` is byte-identical to cli's except CRLF line endings. Read the freshly edited `dotnet/templates/cli/.githooks/pre-commit` and Write its exact content over `dotnet/templates/etl-api/.githooks/pre-commit` (this applies the same four edits and normalizes to LF in one move — CRLF bash hooks are fragile under Git for Windows anyway).

- [ ] **Step 6: Run tests for both templates**

Run: `./dotnet/template-tests/scaffold-and-build.ps1 -Template cli`
Expected: `Smoke test passed.` including both capture sections.

Run: `./dotnet/template-tests/scaffold-and-build.ps1 -Template etl-api`
Expected: same.

- [ ] **Step 7: Commit**

```powershell
git add dotnet/templates/cli/.githooks dotnet/templates/etl-api/.githooks dotnet/template-tests/scaffold-and-build.ps1
git commit -m "Capture guardrail failures from the dotnet template git gates"
```

---

### Task 7: harness-manifest.json for all three templates

**Files:**
- Create: `expo/templates/app/harness-manifest.json`
- Create: `dotnet/templates/cli/harness-manifest.json`
- Create: `dotnet/templates/etl-api/harness-manifest.json`

- [ ] **Step 1: Create the three manifests**

`expo/templates/app/harness-manifest.json`:

```json
{
  "ownedPaths": [
    ".githooks/**",
    ".claude/**",
    "eslint-rules/**",
    "eslint.config.js",
    ".dependency-cruiser.cjs",
    "scripts/**",
    "tsconfig.json",
    ".github/workflows/ci.yml",
    "CLAUDE.md",
    "harness-manifest.json"
  ]
}
```

`dotnet/templates/cli/harness-manifest.json`:

```json
{
  "ownedPaths": [
    ".githooks/**",
    ".claude/**",
    ".editorconfig",
    "tests/.editorconfig",
    "Directory.Build.props",
    "src/ConsoleApp.Analyzers/**",
    "CLAUDE.md",
    "harness-manifest.json"
  ]
}
```

`dotnet/templates/etl-api/harness-manifest.json` — same as cli plus `.codex/**`, with the sourceName `EtlApi`:

```json
{
  "ownedPaths": [
    ".githooks/**",
    ".claude/**",
    ".codex/**",
    ".editorconfig",
    "tests/.editorconfig",
    "Directory.Build.props",
    "src/EtlApi.Analyzers/**",
    "CLAUDE.md",
    "harness-manifest.json"
  ]
}
```

Note: `src/<SourceName>.Analyzers/**` is deliberately the only `src/` path — analyzers are harness infrastructure. The glob is written with the template's sourceName because manifest globs are matched against **template-relative paths in this repo**, before renames are applied. Verify the analyzer project dir exists under each template (`Get-ChildItem dotnet/templates/etl-api/src`) and adjust the glob if the etl-api analyzer project is named differently.

- [ ] **Step 2: Sanity-check JSON parses**

Run: `Get-ChildItem -Recurse -Filter harness-manifest.json | ForEach-Object { (Get-Content $_.FullName -Raw | ConvertFrom-Json).ownedPaths.Count }`
Expected: three numbers (10, 8, 9), no errors.

- [ ] **Step 3: Commit**

```powershell
git add expo/templates/app/harness-manifest.json dotnet/templates/cli/harness-manifest.json dotnet/templates/etl-api/harness-manifest.json
git commit -m "Declare harness-owned paths per template"
```

---

### Task 8: get-update.ps1

**Files:**
- Create: `plugin/scripts/get-update.ps1`
- Create: `plugin/template-tests/update-scripts-test.ps1`

- [ ] **Step 1: Write the failing test**

Create `plugin/template-tests/update-scripts-test.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
if ($null -ne (Get-Variable -Name PSNativeCommandUseErrorActionPreference -Scope Global -ErrorAction SilentlyContinue)) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$pluginScripts = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts'
$work = Join-Path $env:TEMP 'agent-harness-update-test'
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory $work | Out-Null

function Assert([bool]$Cond, [string]$Msg) { if (-not $Cond) { throw "ASSERT FAILED: $Msg" } }

$repo = Join-Path $work 'harness-repo'
New-Item -ItemType Directory $repo | Out-Null
git -C $repo init -q -b main
git -C $repo config user.email t@t.local
git -C $repo config user.name t
$tpl = Join-Path $repo 'expo/templates/app'
New-Item -ItemType Directory (Join-Path $tpl '.githooks') -Force | Out-Null
New-Item -ItemType Directory (Join-Path $tpl 'src') -Force | Out-Null
Set-Content (Join-Path $tpl 'harness-manifest.json') '{ "ownedPaths": [".githooks/**", "CLAUDE.md", "harness-manifest.json"] }'
Set-Content (Join-Path $tpl '.githooks/pre-commit') 'echo AppTemplate v1'
Set-Content (Join-Path $tpl 'CLAUDE.md') '# AppTemplate docs v1'
Set-Content (Join-Path $tpl 'src/index.ts') 'export const appName = "AppTemplate";'
git -C $repo add .
git -C $repo commit -qm v1
$baseCommit = (git -C $repo rev-parse HEAD).Trim()

$proj = Join-Path $work 'MyApp'
robocopy $tpl $proj /E /NFL /NDL /NJH /NJS | Out-Null
$global:LASTEXITCODE = 0
Get-ChildItem $proj -Recurse -File | ForEach-Object {
    $c = [IO.File]::ReadAllText($_.FullName)
    $u = $c.Replace('AppTemplate', 'MyApp')
    if ($u -ne $c) { [IO.File]::WriteAllText($_.FullName, $u) }
}
$stampJson = @"
{
  "template": "expo-app",
  "stack": "expo",
  "repoUrl": "unused-in-tests",
  "templateDir": "expo/templates/app",
  "scaffoldCommit": "$baseCommit",
  "lastUpdateCommit": "$baseCommit",
  "renames": { "AppTemplate": "MyApp", "app-template": "my-app", "apptemplate": "myapp" },
  "scaffoldedAt": "2026-06-11T00:00:00Z"
}
"@
Set-Content (Join-Path $proj '.harness.json') $stampJson

Add-Content (Join-Path $proj 'CLAUDE.md') 'project-specific notes'

Set-Content (Join-Path $tpl '.githooks/pre-commit') 'echo AppTemplate v2'
Set-Content (Join-Path $tpl '.githooks/post-commit') 'echo AppTemplate post'
Set-Content (Join-Path $tpl 'CLAUDE.md') '# AppTemplate docs v2'
Set-Content (Join-Path $tpl 'src/index.ts') 'export const appName = "AppTemplate v2";'
git -C $repo add .
git -C $repo commit -qm v2

$manifestPath = Join-Path $work 'update-manifest.json'
& (Join-Path $pluginScripts 'get-update.ps1') -ProjectDir $proj -RepoPath $repo -OutFile $manifestPath
$m = Get-Content $manifestPath -Raw | ConvertFrom-Json

Assert (@($m.files).Count -eq 3) "expected 3 candidates (src/index.ts is unowned), got $(@($m.files).Count)"
$pre = @($m.files) | Where-Object { $_.projectPath -eq '.githooks/pre-commit' }
Assert ($pre.classification -eq 'clean') "pre-commit should be clean, got '$($pre.classification)'"
$post = @($m.files) | Where-Object { $_.projectPath -eq '.githooks/post-commit' }
Assert ($post.classification -eq 'new') "post-commit should be new, got '$($post.classification)'"
$claude = @($m.files) | Where-Object { $_.projectPath -eq 'CLAUDE.md' }
Assert ($claude.classification -eq 'modified') "CLAUDE.md should be modified, got '$($claude.classification)'"
Assert ($m.headCommit -match '^[0-9a-f]{40}$') 'headCommit recorded'
Assert ($m.lastUpdateCommit -eq $baseCommit) 'lastUpdateCommit recorded'

Write-Host 'Update-script tests passed.'
Remove-Item -Recurse -Force $work
```

(The apply-update assertions are added in Task 9 — keep the final two lines last.)

- [ ] **Step 2: Run test to verify it fails**

Run: `./plugin/template-tests/update-scripts-test.ps1`
Expected: FAIL — `get-update.ps1` not found.

- [ ] **Step 3: Write get-update.ps1**

Create `plugin/scripts/get-update.ps1`:

```powershell
param(
    [string]$ProjectDir = (Get-Location).Path,
    [string]$RepoPath,
    [Parameter(Mandatory = $true)][string]$OutFile
)

$ErrorActionPreference = 'Stop'

function Convert-TemplateText {
    param([string]$Text, $Renames)
    foreach ($p in $Renames.PSObject.Properties) { $Text = $Text.Replace($p.Name, [string]$p.Value) }
    return $Text
}

function Test-OwnedPath {
    param([string]$RelPath, [string[]]$Globs)
    foreach ($g in $Globs) {
        $pattern = [regex]::Escape($g)
        $pattern = $pattern -replace '\\\*\\\*', "\x00"
        $pattern = $pattern -replace '\\\*', '[^/]*'
        $pattern = $pattern -replace "\x00", '.*'
        if ($RelPath -match ('^' + $pattern + '$')) { return $true }
    }
    return $false
}

function Get-Normalized {
    param([string]$Text)
    return ($Text -replace "`r`n", "`n").TrimEnd("`n")
}

$stampPath = Join-Path $ProjectDir '.harness.json'
if (-not (Test-Path $stampPath)) { throw "No .harness.json in $ProjectDir. Run the harness-update skill's backfill flow first." }
$stamp = Get-Content $stampPath -Raw | ConvertFrom-Json

if (-not $RepoPath) {
    $RepoPath = & (Join-Path $PSScriptRoot 'resolve-repo.ps1') -RepoUrl $stamp.repoUrl | Select-Object -Last 1
}

$headCommit = (git -C $RepoPath rev-parse origin/main 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0) {
    $headCommit = (git -C $RepoPath rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Could not resolve a head commit in $RepoPath" }
}

git -C $RepoPath cat-file -e "$($stamp.lastUpdateCommit)^{commit}"
if ($LASTEXITCODE -ne 0) { throw "Stamped commit $($stamp.lastUpdateCommit) not found in $RepoPath (shallow clone? run 'git fetch --unshallow')" }

$templateDir = $stamp.templateDir
$manifestRaw = (git -C $RepoPath show "${headCommit}:$templateDir/harness-manifest.json") -join "`n"
if ($LASTEXITCODE -ne 0) { throw "harness-manifest.json missing from $templateDir at $headCommit" }
$ownedGlobs = [string[]]((($manifestRaw | ConvertFrom-Json).ownedPaths))

$diffLines = @(git -C $RepoPath diff --name-status "$($stamp.lastUpdateCommit)..$headCommit" -- $templateDir)
if ($LASTEXITCODE -ne 0) { throw 'git diff failed' }

$files = @()
foreach ($line in $diffLines) {
    if (-not $line) { continue }
    $parts = $line -split "`t"
    $status = $parts[0]
    $templatePath = if ($status -like 'R*') { $parts[2] } else { $parts[1] }
    $relPath = $templatePath.Substring($templateDir.Length + 1)
    if (-not (Test-OwnedPath -RelPath $relPath -Globs $ownedGlobs)) { continue }

    $projectRel = Convert-TemplateText -Text $relPath -Renames $stamp.renames
    $projectFile = Join-Path $ProjectDir $projectRel

    if ($status -eq 'D') {
        $class = if (Test-Path $projectFile) { 'deleted' } else { 'already-absent' }
    }
    elseif (-not (Test-Path $projectFile)) {
        $class = 'new'
    }
    else {
        $baseRaw = (git -C $RepoPath show "$($stamp.lastUpdateCommit):$templatePath" 2>&1) -join "`n"
        if ($LASTEXITCODE -ne 0) { $baseRaw = $null }
        $current = Get-Content $projectFile -Raw
        if ($null -ne $baseRaw -and (Get-Normalized (Convert-TemplateText -Text $baseRaw -Renames $stamp.renames)) -eq (Get-Normalized $current)) {
            $class = 'clean'
        }
        else {
            $class = 'modified'
        }
    }
    $files += [pscustomobject]@{
        templatePath   = $templatePath
        projectPath    = $projectRel
        gitStatus      = $status
        classification = $class
    }
}

$result = [pscustomobject]@{
    projectDir       = $ProjectDir
    repoPath         = $RepoPath
    templateDir      = $templateDir
    lastUpdateCommit = $stamp.lastUpdateCommit
    headCommit       = $headCommit
    files            = @($files)
}
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[IO.File]::WriteAllText($OutFile, ($result | ConvertTo-Json -Depth 5), $utf8NoBom)
Write-Output "Wrote update manifest to $OutFile ($(@($files).Count) candidate file(s), head $headCommit)"
```

Implementation notes baked in above:
- `(git show ...) -join "`n"` instead of `Out-String` — `Out-String` rejoins with CRLF and corrupts the comparison.
- Content comparison normalizes line endings and trailing newlines (`Get-Normalized`) because scaffolded working copies may be CRLF while the repo stores LF.
- The `2>&1` on `git rev-parse origin/main` is intentional and safe here: failure is expected when the fixture repo has no remote, and we immediately overwrite `$headCommit` on the fallback path. If PowerShell 5.1 raises a NativeCommandError instead, wrap that single call in `try { } catch { $global:LASTEXITCODE = 1 }` — the fallback logic is the contract, the mechanism may need that adjustment during execution.

- [ ] **Step 4: Run test to verify it passes**

Run: `./plugin/template-tests/update-scripts-test.ps1`
Expected: `Update-script tests passed.`

- [ ] **Step 5: Commit**

```powershell
git add plugin/scripts/get-update.ps1 plugin/template-tests/update-scripts-test.ps1
git commit -m "Add get-update script classifying harness update candidates"
```

---

### Task 9: apply-update.ps1

**Files:**
- Create: `plugin/scripts/apply-update.ps1`
- Modify: `plugin/template-tests/update-scripts-test.ps1`

- [ ] **Step 1: Extend the test (failing first)**

In `plugin/template-tests/update-scripts-test.ps1`, insert before the final `Write-Host 'Update-script tests passed.'`:

```powershell
& (Join-Path $pluginScripts 'apply-update.ps1') -ManifestPath $manifestPath
Assert ((Get-Content (Join-Path $proj '.githooks/pre-commit') -Raw) -match 'MyApp v2') 'clean file updated with rename applied'
Assert (Test-Path (Join-Path $proj '.githooks/post-commit')) 'new file added'
Assert ((Get-Content (Join-Path $proj '.githooks/post-commit') -Raw) -match 'MyApp post') 'new file renamed'
$claudeContent = Get-Content (Join-Path $proj 'CLAUDE.md') -Raw
Assert ($claudeContent -match 'project-specific notes') 'modified file left untouched'
Assert ($claudeContent -notmatch 'docs v2') 'modified file not overwritten'
Assert ((Get-Content (Join-Path $proj 'src/index.ts') -Raw) -match 'MyApp";') 'unowned src file untouched'

& (Join-Path $pluginScripts 'apply-update.ps1') -ManifestPath $manifestPath
Assert ((Get-Content (Join-Path $proj '.githooks/pre-commit') -Raw) -match 'MyApp v2') 'apply-update is idempotent'
```

- [ ] **Step 2: Run test to verify it fails**

Run: `./plugin/template-tests/update-scripts-test.ps1`
Expected: FAIL — `apply-update.ps1` not found.

- [ ] **Step 3: Write apply-update.ps1**

Create `plugin/scripts/apply-update.ps1`:

```powershell
param(
    [Parameter(Mandatory = $true)][string]$ManifestPath
)

$ErrorActionPreference = 'Stop'

function Convert-TemplateText {
    param([string]$Text, $Renames)
    foreach ($p in $Renames.PSObject.Properties) { $Text = $Text.Replace($p.Name, [string]$p.Value) }
    return $Text
}

$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$stamp = Get-Content (Join-Path $manifest.projectDir '.harness.json') -Raw | ConvertFrom-Json
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

$applied = @()
$flagged = @()
foreach ($f in @($manifest.files)) {
    if ($f.classification -eq 'clean' -or $f.classification -eq 'new') {
        $content = (git -C $manifest.repoPath show "$($manifest.headCommit):$($f.templatePath)") -join "`n"
        if ($LASTEXITCODE -ne 0) { throw "git show failed for $($f.templatePath)" }
        $content = Convert-TemplateText -Text $content -Renames $stamp.renames
        if (-not $content.EndsWith("`n")) { $content += "`n" }
        $target = Join-Path $manifest.projectDir $f.projectPath
        $targetDir = Split-Path -Parent $target
        if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Force $targetDir | Out-Null }
        [IO.File]::WriteAllText($target, $content, $utf8NoBom)
        $applied += $f.projectPath
    }
    elseif ($f.classification -eq 'modified' -or $f.classification -eq 'deleted') {
        $flagged += $f
    }
}

Write-Output "Applied $($applied.Count) file(s):"
$applied | ForEach-Object { Write-Output "  $_" }
Write-Output "Flagged for manual merge: $(@($flagged).Count)"
$flagged | ForEach-Object { Write-Output "  $($_.projectPath) [$($_.classification)]" }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `./plugin/template-tests/update-scripts-test.ps1`
Expected: `Update-script tests passed.`

- [ ] **Step 5: Commit**

```powershell
git add plugin/scripts/apply-update.ps1 plugin/template-tests/update-scripts-test.ps1
git commit -m "Add apply-update script writing clean and new harness files"
```

---

### Task 10: Scaffolding skills (three SKILL.md files)

**Files:**
- Create: `plugin/skills/new-expo-app/SKILL.md`
- Create: `plugin/skills/new-dotnet-cli/SKILL.md`
- Create: `plugin/skills/new-dotnet-etl-api/SKILL.md`

- [ ] **Step 1: Create new-expo-app/SKILL.md**

```markdown
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
```

- [ ] **Step 2: Create new-dotnet-cli/SKILL.md**

```markdown
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
```

- [ ] **Step 3: Create new-dotnet-etl-api/SKILL.md**

Same as Step 2 with these substitutions: frontmatter `name: new-dotnet-etl-api`, description `Use when creating a new .NET ETL/API service from the agent-harness template ("new api", "new etl service", "new backend service"). Scaffolds, stamps provenance, and verifies guardrails.`, title `# New .NET ETL API Project`, first paragraph references the `etl-api` template, scaffold command `dotnet new etl-api -n <Name> -o <dir>`, stamp command `-Template etl-api`. Full file:

```markdown
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
```

- [ ] **Step 4: Validate**

Run: `./plugin/template-tests/validate-plugin.ps1`
Expected: `Plugin validation passed.` (frontmatter names match directory names).

- [ ] **Step 5: Commit**

```powershell
git add plugin/skills/new-expo-app plugin/skills/new-dotnet-cli plugin/skills/new-dotnet-etl-api
git commit -m "Add scaffolding skills for the three templates"
```

---

### Task 11: harness-update skill

**Files:**
- Create: `plugin/skills/harness-update/SKILL.md`

- [ ] **Step 1: Create harness-update/SKILL.md**

```markdown
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
```

- [ ] **Step 2: Validate**

Run: `./plugin/template-tests/validate-plugin.ps1 -RequireFull`
Expected: `Plugin validation passed.` — all four skills, hooks.json, and all five scripts now exist.

- [ ] **Step 3: Commit**

```powershell
git add plugin/skills/harness-update
git commit -m "Add harness-update skill orchestrating template syncs"
```

---

### Task 12: CLAUDE.md awareness sections in all three templates

**Files:**
- Modify: `expo/templates/app/CLAUDE.md`
- Modify: `dotnet/templates/cli/CLAUDE.md`
- Modify: `dotnet/templates/etl-api/CLAUDE.md`

- [ ] **Step 1: Append the section to all three files**

Append to the END of each template CLAUDE.md (identical text; it lands in every scaffolded project):

```markdown

## Harness maintenance

This project was scaffolded from the agent-harness template repo.
`.harness.json` records the template and commit it came from — never
hand-edit or delete it.

- **Updating the harness.** On any request like "update the harness",
  "pull the latest template changes", or "update the guardrails", invoke
  the `agent-harness:harness-update` skill (ships with the agent-harness
  plugin). It updates only harness-owned files — hooks, lint rules,
  analyzers, CI, this file — never your app code, and goes through the
  normal issue → branch → PR lifecycle.
- **Feedback events.** When a blocked commit prints
  `HARNESS-FEEDBACK: event <id>`, append a one-line note describing what
  the failing code was trying to do (the agent-harness plugin injects the
  exact `harness-note.ps1` command), then fix the failure and commit
  again as normal.
```

- [ ] **Step 2: Verify the expo template test still passes the lint gate**

The CLAUDE.md edit is markdown (not linted), but run a quick scaffold sanity check that nothing else broke:

Run: `Select-String -Path expo/templates/app/CLAUDE.md,dotnet/templates/cli/CLAUDE.md,dotnet/templates/etl-api/CLAUDE.md -Pattern 'Harness maintenance' | Measure-Object | Select-Object -ExpandProperty Count`
Expected: `3`

- [ ] **Step 3: Commit**

```powershell
git add expo/templates/app/CLAUDE.md dotnet/templates/cli/CLAUDE.md dotnet/templates/etl-api/CLAUDE.md
git commit -m "Document harness maintenance in template CLAUDE.md files"
```

---

### Task 13: CI job + README

**Files:**
- Modify: `.github/workflows/template-ci.yml`
- Modify: `README.md`

- [ ] **Step 1: Add the plugin-validate job**

Append to `.github/workflows/template-ci.yml` (same indentation as the existing jobs):

```yaml
  plugin-validate:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Plugin manifest and skill sanity
        shell: pwsh
        run: ./plugin/template-tests/validate-plugin.ps1 -RequireFull

      - name: Stamp and resolve-repo tests
        shell: pwsh
        run: ./plugin/template-tests/stamp-test.ps1

      - name: Annotation hook tests
        shell: pwsh
        run: ./plugin/template-tests/annotate-test.ps1

      - name: Update-script tests
        shell: pwsh
        run: ./plugin/template-tests/update-scripts-test.ps1
```

- [ ] **Step 2: Add the plugin section to README.md**

Insert before the `## CI` section:

```markdown
## Claude Code plugin

The repo doubles as a Claude Code plugin marketplace. The `agent-harness`
plugin (in [`plugin/`](plugin/)) ships:

- **Scaffolding skills** — `new-dotnet-cli`, `new-dotnet-etl-api`,
  `new-expo-app` — which scaffold from the templates above, stamp the
  project with `.harness.json` provenance, and verify the guardrails pass.
- **`harness-update`** — pulls the latest template changes into a
  previously-scaffolded project: PowerShell scripts classify each changed
  harness-owned file (clean → copied, customized → flagged), the agent
  merges the flagged ones, and the project's own gate must pass before
  the stamp bumps.
- **Feedback capture** — the templates' git hooks log every pre-commit
  gate failure (which gate, output tail, staged diff) to
  `~/.agent-harness/feedback/`, link the eventual fix commit, and the
  plugin prompts the agent to annotate each event. Raw material for
  turning recurring mistakes into new guardrails.

Install:

```text
/plugin marketplace add ryan75195/dotnet-agent-harness
/plugin install agent-harness@agent-harness
```

Local development: `claude --plugin-dir .\plugin`. A local checkout can be
pinned for offline scaffolds/updates in `~/.agent-harness/config.json`:
`{ "repoPath": "C:/path/to/dotnet-agent-harness" }`.
```

- [ ] **Step 3: Run the full local test suite one final time**

```powershell
./plugin/template-tests/validate-plugin.ps1 -RequireFull
./plugin/template-tests/stamp-test.ps1
./plugin/template-tests/annotate-test.ps1
./plugin/template-tests/update-scripts-test.ps1
```
Expected: all four pass.

- [ ] **Step 4: Commit**

```powershell
git add .github/workflows/template-ci.yml README.md
git commit -m "Add plugin CI job and README documentation"
```

---

## Post-implementation verification (manual, once)

Not part of CI — worth one manual pass before calling the feature done:

1. `claude --plugin-dir .\plugin` in a scratch directory → `/agent-harness:new-expo-app` → confirm scaffold + stamp + passing verify.
2. In that scaffold, seed a `// comment` in `src/lib/`, attempt a commit, confirm the agent receives the annotation instruction (plugin hook) and `~/.agent-harness/feedback/events.jsonl` gains the event + note + fix linkage.
3. Touch a harness-owned file in the template repo (e.g. add an echo to the expo pre-commit), commit to a scratch branch, point `~/.agent-harness/config.json` at the checkout, and run `/agent-harness:harness-update` in the scaffold → confirm classify/apply/verify/stamp-bump.
```
