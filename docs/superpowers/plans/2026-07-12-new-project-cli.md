# `new-project` CLI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A standalone, on-PATH, flags-only `new-project` command that scaffolds any template type (expo, dotnet-cli, dotnet-etl-api) with no agent involved, owning the scaffold→stamp→setup→verify pipeline, with the three plugin skills refactored to delegate to it.

**Architecture:** A dispatcher (`new-project.ps1`, repo root) self-locates via `$PSScriptRoot`, dot-sources a pure library (`new-project/lib.ps1`: discovery, validation, planning, usage), discovers per-type descriptors from `new-project/handlers/*.ps1`, and runs a common pipeline. An `install.ps1` puts shims on PATH. The plugin skills become thin delegates.

**Tech Stack:** Windows PowerShell 5.1-compatible scripts; plain-PowerShell throw-on-failure test scripts (matching existing `template-tests`); reuses `plugin/scripts/write-stamp.ps1` and the per-type scaffolders (`expo/new-app.ps1`, `dotnet new`).

## Global Constraints

- **Windows PowerShell 5.1 compatible.** Shims invoke `powershell.exe` (always present). No pwsh-7-only syntax.
- **Self-locating, no installed-plugin dependency.** `new-project.ps1` uses `$PSScriptRoot` as the repo root and reaches `expo/new-app.ps1`, `dotnet/`, `plugin/scripts/write-stamp.ps1`, and `new-project/handlers/` by repo-relative path.
- **Flags-only, non-interactive.** `new-project <type> <Name> [flags]`. No wizard. Usage on no args / `-h` / `--help`; usage + exit 2 on validation error.
- **Extras splatted, only when provided.** Unset optional flags (e.g. `-BundleId`) must be OMITTED, never passed as `$null`, so the scaffolder's own default applies. `write-stamp.ps1 -BundleId` is passed only when the user supplied it.
- **Name rule:** PascalCase `^[A-Z][A-Za-z0-9]*$` for all types.
- **Destination:** default `<cwd>/<Name>`; error if it already exists.
- **Pipeline order (dispatcher owns it):** `PreInstall?` → `Scaffold` → `write-stamp.ps1` → `cd $Dest; ./setup.ps1` → (if `.harness.json` untracked, `git add`+`git commit --no-verify`) → `Verify` → report. Any failure → stderr message + non-zero exit.
- **`write-stamp.ps1 -Template` values (from handler `StampName`):** `expo-app`, `cli`, `etl-api` (the script's validated set).
- **Error output to stderr.** Use `[Console]::Error.WriteLine(...)` then `exit N` for user-facing errors (never `Write-Error` — `$ErrorActionPreference='Stop'` would make it throw and skip the exit code).
- **Tests** are plain-PowerShell scripts that `throw` on mismatch and print a pass line, matching `expo/template-tests` / `plugin/template-tests` style.
- **Out of scope:** interactive wizard, global CLAUDE.md, new project types, config UI.

This plan edits the repo on branch `feat/new-project-cli`. Run PowerShell test/verify steps with the PowerShell tool (Windows PowerShell 5.1) or `powershell -NoProfile -ExecutionPolicy Bypass -File <path>`. Commit from the repo root `C:/Users/ryan7/programming/agent-project-templates`.

---

### Task 1: Handlers + pure library (discovery, validation, planning, usage)

**Files:**
- Create: `new-project/lib.ps1`
- Create: `new-project/handlers/expo.ps1`
- Create: `new-project/handlers/dotnet-cli.ps1`
- Create: `new-project/handlers/dotnet-etl-api.ps1`
- Test: `new-project/tests/lib-tests.ps1`

**Interfaces:**
- Produces (dot-sourceable from `new-project/lib.ps1`):
  - `Get-NewProjectHandlers -HandlersDir <dir>` → `array` of descriptor hashtables (each has `Type`, `Description`, `StampName`, `ExtraArgs`, `PreInstall`, `Scaffold`, `Verify`).
  - `Format-NewProjectUsage -Handlers <array>` → `string`.
  - `Resolve-NewProjectPlan -Handlers <array> -Type <s> -Name <s> [-Flags <hashtable>] -Cwd <s>` → plan hashtable `@{ Type; Handler; Name; Dest; Extra; Steps }`, or throws a validation error.
  - `Format-NewProjectPlan -Plan <hashtable>` → `string`.
- A handler descriptor's `ExtraArgs` is an array of `@{ Name; Required; Help }`. `$ctx` (built by the dispatcher in Task 2) carries `Repo`, `Name`, `Dest`, `Extra` (hashtable of provided extras).

- [ ] **Step 1: Write the failing test**

Create `new-project/tests/lib-tests.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repo 'new-project/lib.ps1')
$handlers = Get-NewProjectHandlers -HandlersDir (Join-Path $repo 'new-project/handlers')

function Assert($cond, $msg) { if (-not $cond) { throw "FAIL: $msg" } }
function AssertThrows($script, $match, $msg) {
    $threw = $false
    try { & $script } catch { $threw = $true; if ($match -and $_.Exception.Message -notmatch $match) { throw "FAIL: $msg - wrong error: $($_.Exception.Message)" } }
    if (-not $threw) { throw "FAIL: $msg - expected a throw" }
}

$types = ($handlers | ForEach-Object { $_.Type }) | Sort-Object
Assert (($types -join ',') -eq 'dotnet-cli,dotnet-etl-api,expo') "discovery finds three types, got $($types -join ',')"

$usage = Format-NewProjectUsage -Handlers $handlers
foreach ($t in 'expo', 'dotnet-cli', 'dotnet-etl-api') { Assert ($usage -match [regex]::Escape($t)) "usage lists $t" }
Assert ($usage -match 'BundleId') 'usage shows expo BundleId flag'

$plan = Resolve-NewProjectPlan -Handlers $handlers -Type 'expo' -Name 'MyApp' -Flags @{ BundleId = 'com.acme.myapp' } -Cwd 'C:/tmp'
Assert ($plan.Dest -eq (Join-Path 'C:/tmp' 'MyApp')) "expo default dest, got $($plan.Dest)"
Assert ($plan.Extra.BundleId -eq 'com.acme.myapp') 'expo carries BundleId'

$plan2 = Resolve-NewProjectPlan -Handlers $handlers -Type 'dotnet-cli' -Name 'MyTool' -Cwd 'C:/tmp'
Assert ($plan2.Extra.Count -eq 0) 'dotnet-cli has no extras'

AssertThrows { Resolve-NewProjectPlan -Handlers $handlers -Type 'nope' -Name 'X' -Cwd 'C:/tmp' } 'Unknown project type' 'unknown type errors'
AssertThrows { Resolve-NewProjectPlan -Handlers $handlers -Type 'expo' -Name '' -Cwd 'C:/tmp' } 'required' 'missing name errors'
AssertThrows { Resolve-NewProjectPlan -Handlers $handlers -Type 'expo' -Name 'lowerbad' -Cwd 'C:/tmp' } 'PascalCase' 'non-pascal name errors'
AssertThrows { Resolve-NewProjectPlan -Handlers $handlers -Type 'dotnet-cli' -Name 'X' -Flags @{ BundleId = 'y' } -Cwd 'C:/tmp' } 'not valid for type' 'undeclared flag errors'

$dry = Format-NewProjectPlan -Plan $plan2
Assert ($dry -match 'dotnet-cli') 'plan formats type'
Assert ($dry -match 'scaffold -> stamp') 'plan lists steps in order'

Write-Host 'lib-tests: all passed'
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File new-project/tests/lib-tests.ps1`
Expected: FAIL — `lib.ps1` / handlers do not exist yet (cannot dot-source).

- [ ] **Step 3: Create the three handler files**

`new-project/handlers/expo.ps1`:

```powershell
@{
    Type        = 'expo'
    Description = 'Expo / React Native app (payments, auth, EAS build/deploy)'
    StampName   = 'expo-app'
    ExtraArgs   = @(
        @{ Name = 'BundleId'; Required = $false; Help = 'reverse-DNS id, e.g. com.acme.myapp' }
    )
    PreInstall  = $null
    Scaffold    = {
        param($ctx)
        $extra = $ctx.Extra
        & "$($ctx.Repo)/expo/new-app.ps1" -Name $ctx.Name -Destination $ctx.Dest @extra
    }
    Verify      = {
        param($ctx)
        npm run verify
        if ($LASTEXITCODE) { throw "npm run verify failed (exit $LASTEXITCODE)" }
    }
}
```

`new-project/handlers/dotnet-cli.ps1`:

```powershell
@{
    Type        = 'dotnet-cli'
    Description = '.NET 10 CLI/console solution (Roslyn analyzers + architecture tests)'
    StampName   = 'cli'
    ExtraArgs   = @()
    PreInstall  = {
        param($ctx)
        dotnet new install "$($ctx.Repo)/dotnet" --force
        if ($LASTEXITCODE) { throw "dotnet new install failed (exit $LASTEXITCODE)" }
    }
    Scaffold    = {
        param($ctx)
        dotnet new cli -n $ctx.Name -o $ctx.Dest
        if ($LASTEXITCODE) { throw "dotnet new cli failed (exit $LASTEXITCODE)" }
    }
    Verify      = {
        param($ctx)
        dotnet build --no-incremental
        if ($LASTEXITCODE) { throw "dotnet build failed (exit $LASTEXITCODE)" }
        dotnet test --no-build --verbosity minimal
        if ($LASTEXITCODE) { throw "dotnet test failed (exit $LASTEXITCODE)" }
    }
}
```

`new-project/handlers/dotnet-etl-api.ps1`:

```powershell
@{
    Type        = 'dotnet-etl-api'
    Description = '.NET 10 ETL/API service (Roslyn analyzers + architecture tests, agent configs)'
    StampName   = 'etl-api'
    ExtraArgs   = @()
    PreInstall  = {
        param($ctx)
        dotnet new install "$($ctx.Repo)/dotnet" --force
        if ($LASTEXITCODE) { throw "dotnet new install failed (exit $LASTEXITCODE)" }
    }
    Scaffold    = {
        param($ctx)
        dotnet new etl-api -n $ctx.Name -o $ctx.Dest
        if ($LASTEXITCODE) { throw "dotnet new etl-api failed (exit $LASTEXITCODE)" }
    }
    Verify      = {
        param($ctx)
        dotnet build --no-incremental
        if ($LASTEXITCODE) { throw "dotnet build failed (exit $LASTEXITCODE)" }
        dotnet test --no-build --verbosity minimal
        if ($LASTEXITCODE) { throw "dotnet test failed (exit $LASTEXITCODE)" }
    }
}
```

- [ ] **Step 4: Create the library**

`new-project/lib.ps1`:

```powershell
function Get-NewProjectHandlers {
    param([Parameter(Mandatory)][string]$HandlersDir)
    if (-not (Test-Path $HandlersDir)) { throw "Handlers dir not found: $HandlersDir" }
    $handlers = @()
    foreach ($file in Get-ChildItem -Path $HandlersDir -Filter '*.ps1' | Sort-Object Name) {
        $descriptor = & $file.FullName
        if (-not $descriptor -or -not $descriptor.Type) {
            throw "Handler $($file.Name) did not return a descriptor with a Type"
        }
        $handlers += $descriptor
    }
    return $handlers
}

function Format-NewProjectUsage {
    param([Parameter(Mandatory)][array]$Handlers)
    $lines = @('Usage: new-project <type> <Name> [flags]', '', 'Types:')
    foreach ($h in $Handlers) {
        $lines += "  $($h.Type)  -  $($h.Description)"
        $flagParts = @('-Destination <dir>')
        foreach ($a in @($h.ExtraArgs)) {
            $suffix = if ($a.Required) { '' } else { '?' }
            $flagParts += "-$($a.Name)$suffix <value>"
        }
        $lines += "      flags: $($flagParts -join '  ')"
    }
    $lines += @('', 'Name must be PascalCase (^[A-Z][A-Za-z0-9]*$).', 'Run with -DryRun to preview the plan without scaffolding.')
    return ($lines -join "`n")
}

function Resolve-NewProjectPlan {
    param(
        [Parameter(Mandatory)][array]$Handlers,
        [string]$Type,
        [string]$Name,
        [hashtable]$Flags = @{},
        [Parameter(Mandatory)][string]$Cwd
    )
    if (-not $Type) { throw 'No project type given. Run new-project with no args for usage.' }
    $handler = $Handlers | Where-Object { $_.Type -eq $Type } | Select-Object -First 1
    if (-not $handler) { throw "Unknown project type '$Type'. Known types: $((($Handlers | ForEach-Object { $_.Type })) -join ', ')." }
    if (-not $Name) { throw "Project name is required: new-project $Type <Name> [flags]" }
    if ($Name -notmatch '^[A-Z][A-Za-z0-9]*$') { throw "Name '$Name' must be PascalCase (^[A-Z][A-Za-z0-9]*$)." }

    $declared = @{}
    foreach ($a in @($handler.ExtraArgs)) { $declared[$a.Name] = $a }

    $extra = @{}
    foreach ($key in $Flags.Keys) {
        if ($key -eq 'Destination') { continue }
        if (-not $declared.ContainsKey($key)) { throw "Flag -$key is not valid for type '$Type'." }
        $extra[$key] = $Flags[$key]
    }
    foreach ($a in @($handler.ExtraArgs)) {
        if ($a.Required -and -not $extra.ContainsKey($a.Name)) { throw "Flag -$($a.Name) is required for type '$Type'." }
    }

    $dest = if ($Flags.ContainsKey('Destination') -and $Flags['Destination']) { $Flags['Destination'] } else { Join-Path $Cwd $Name }

    $steps = @()
    if ($handler.PreInstall) { $steps += 'pre-install templates' }
    $steps += @('scaffold', 'stamp provenance (.harness.json)', 'setup (git init, hooks, initial commit)', 'verify')

    return @{ Type = $Type; Handler = $handler; Name = $Name; Dest = $dest; Extra = $extra; Steps = $steps }
}

function Format-NewProjectPlan {
    param([Parameter(Mandatory)][hashtable]$Plan)
    $lines = @("Plan for new-project $($Plan.Type) $($Plan.Name):", "  destination: $($Plan.Dest)")
    foreach ($k in $Plan.Extra.Keys) { $lines += "  $($k): $($Plan.Extra[$k])" }
    $lines += "  steps: $($Plan.Steps -join ' -> ')"
    return ($lines -join "`n")
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File new-project/tests/lib-tests.ps1`
Expected: `lib-tests: all passed`.

- [ ] **Step 6: Commit**

```bash
git add new-project/lib.ps1 new-project/handlers new-project/tests/lib-tests.ps1
git commit -m "Add new-project handlers and pure dispatcher library"
```

---

### Task 2: Dispatcher entry point + execution pipeline

**Files:**
- Create: `new-project.ps1`
- Test: `new-project/tests/e2e-dotnet-cli.ps1`

**Interfaces:**
- Consumes: `new-project/lib.ps1` functions and `new-project/handlers/*.ps1` from Task 1; `plugin/scripts/write-stamp.ps1`.
- Produces: the `new-project` command — parses `<type> <Name> [flags]` from `$args`, prints usage / `-DryRun` plan, or runs the pipeline. Exit codes: 0 success/usage, 1 pipeline failure / dest exists, 2 validation error.

- [ ] **Step 1: Write the failing end-to-end test**

Create `new-project/tests/e2e-dotnet-cli.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$dest = Join-Path $env:TEMP 'new-project-e2e-cli'
if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }

& (Join-Path $repo 'new-project.ps1') dotnet-cli NpE2ETool -Destination $dest
if ($LASTEXITCODE -ne 0) { throw "new-project exited $LASTEXITCODE" }
if (-not (Test-Path $dest)) { throw 'project dir was not created' }
$stampPath = Join-Path $dest '.harness.json'
if (-not (Test-Path $stampPath)) { throw '.harness.json missing' }
$stamp = Get-Content $stampPath -Raw | ConvertFrom-Json
if ($stamp.template -ne 'cli') { throw "stamp template is not cli: $($stamp.template)" }

Remove-Item -Recurse -Force $dest
Write-Host 'e2e-dotnet-cli: passed'
```

(A successful exit 0 means PreInstall + scaffold + stamp + setup + verify all passed, so no separate re-build is needed. Requires the dotnet SDK and a configured git identity — both present in this environment.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File new-project/tests/e2e-dotnet-cli.ps1`
Expected: FAIL — `new-project.ps1` does not exist yet.

- [ ] **Step 3: Create the dispatcher**

`new-project.ps1` (note: NO `param()` block — the shim uses `powershell -File`, so all tokens including `-flags` arrive in the automatic `$args`):

```powershell
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'new-project/lib.ps1')
$handlers = Get-NewProjectHandlers -HandlersDir (Join-Path $PSScriptRoot 'new-project/handlers')

$argv = @($args)
$positional = @()
$flags = @{}
$dryRun = $false
$help = $false

try {
    $i = 0
    while ($i -lt $argv.Count) {
        $tok = $argv[$i]
        if ($tok -match '^(-h|--help|-Help)$') { $help = $true; $i++ }
        elseif ($tok -eq '-DryRun') { $dryRun = $true; $i++ }
        elseif ($tok -match '^-(Destination|Dest)$') { $flags['Destination'] = $argv[$i + 1]; $i += 2 }
        elseif ($tok -eq '-BundleId') { $flags['BundleId'] = $argv[$i + 1]; $i += 2 }
        elseif ($tok -like '-*') { throw "Unknown flag '$tok'." }
        else { $positional += $tok; $i++ }
    }

    if ($help -or $argv.Count -eq 0) {
        Write-Host (Format-NewProjectUsage -Handlers $handlers)
        exit 0
    }

    $type = $positional[0]
    $name = if ($positional.Count -ge 2) { $positional[1] } else { $null }
    $plan = Resolve-NewProjectPlan -Handlers $handlers -Type $type -Name $name -Flags $flags -Cwd (Get-Location).Path
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    [Console]::Error.WriteLine('')
    [Console]::Error.WriteLine((Format-NewProjectUsage -Handlers $handlers))
    exit 2
}

if ($dryRun) {
    Write-Host (Format-NewProjectPlan -Plan $plan)
    exit 0
}

$ctx = @{ Repo = $PSScriptRoot; Name = $plan.Name; Dest = $plan.Dest; Extra = $plan.Extra }
$handler = $plan.Handler

if (Test-Path $ctx.Dest) {
    [Console]::Error.WriteLine("Destination already exists: $($ctx.Dest)")
    exit 1
}

try {
    if ($handler.PreInstall) { & $handler.PreInstall $ctx }
    & $handler.Scaffold $ctx

    $stampArgs = @{ ProjectDir = $ctx.Dest; Template = $handler.StampName; ProjectName = $ctx.Name; RepoPath = $ctx.Repo }
    if ($ctx.Extra.ContainsKey('BundleId')) { $stampArgs['BundleId'] = $ctx.Extra['BundleId'] }
    & (Join-Path $PSScriptRoot 'plugin/scripts/write-stamp.ps1') @stampArgs

    Push-Location $ctx.Dest
    try {
        & (Join-Path $ctx.Dest 'setup.ps1')
        if ($LASTEXITCODE) { throw "setup.ps1 failed (exit $LASTEXITCODE) - see the message above (commonly a missing git identity)." }

        $stampStatus = git status --porcelain -- .harness.json
        if ($stampStatus) {
            git add .harness.json
            git commit --no-verify -q -m 'Add harness provenance stamp'
        }
        $global:LASTEXITCODE = 0

        & $handler.Verify $ctx
    }
    finally { Pop-Location }
}
catch {
    [Console]::Error.WriteLine("new-project failed: $($_.Exception.Message)")
    exit 1
}

Write-Host ''
Write-Host "Created $($plan.Type) project '$($ctx.Name)' at $($ctx.Dest)"
Write-Host 'Guardrails are active (see the project CLAUDE.md). Next: gh repo create, then follow the dev lifecycle.'
exit 0
```

- [ ] **Step 4: Run the end-to-end test to verify it passes**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File new-project/tests/e2e-dotnet-cli.ps1`
Expected: `e2e-dotnet-cli: passed`.

Then sanity-check usage and dry-run (no side effects):

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File new-project.ps1`
Expected: usage text listing `expo`, `dotnet-cli`, `dotnet-etl-api`.

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File new-project.ps1 expo MyApp -BundleId com.acme.myapp -DryRun`
Expected: a plan block showing `destination:` `.../MyApp`, `BundleId: com.acme.myapp`, and `steps: scaffold -> stamp ... -> verify`.

- [ ] **Step 5: Commit**

```bash
git add new-project.ps1 new-project/tests/e2e-dotnet-cli.ps1
git commit -m "Add new-project dispatcher entry point and execution pipeline"
```

---

### Task 3: `install.ps1` — put `new-project` on PATH

**Files:**
- Create: `install.ps1`
- Test: `new-project/tests/install-tests.ps1`

**Interfaces:**
- Produces: `install.ps1` with params `-BinDir <dir>` (default `~/.agent-harness/bin`), `-NoPath`, `-NoConfig`. Writes `new-project.cmd` and `new-project` (sh) shims into `BinDir`, both invoking `powershell -NoProfile -ExecutionPolicy Bypass -File "<repo>\new-project.ps1"` with args; adds `BinDir` to User PATH unless `-NoPath`; sets `~/.agent-harness/config.json` `repoPath` to this checkout only if unset, unless `-NoConfig`.

- [ ] **Step 1: Write the failing test**

Create `new-project/tests/install-tests.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$bin = Join-Path $env:TEMP 'np-install-test-bin'
if (Test-Path $bin) { Remove-Item -Recurse -Force $bin }

& (Join-Path $repo 'install.ps1') -BinDir $bin -NoPath -NoConfig | Out-Null

foreach ($f in 'new-project.cmd', 'new-project') {
    if (-not (Test-Path (Join-Path $bin $f))) { throw "shim '$f' was not created" }
}
$cmd = Get-Content (Join-Path $bin 'new-project.cmd') -Raw
if ($cmd -notmatch 'new-project\.ps1') { throw 'cmd shim does not reference new-project.ps1' }
if ($cmd -notmatch [regex]::Escape(($repo -replace '/', '\'))) { throw 'cmd shim does not reference the repo path' }
$sh = Get-Content (Join-Path $bin 'new-project') -Raw
if ($sh -notmatch 'new-project\.ps1') { throw 'sh shim does not reference new-project.ps1' }

Remove-Item -Recurse -Force $bin
Write-Host 'install-tests: passed'
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File new-project/tests/install-tests.ps1`
Expected: FAIL — `install.ps1` does not exist yet.

- [ ] **Step 3: Create the installer**

`install.ps1`:

```powershell
[CmdletBinding()]
param(
    [string]$BinDir = (Join-Path $HOME '.agent-harness/bin'),
    [switch]$NoPath,
    [switch]$NoConfig
)
$ErrorActionPreference = 'Stop'

$repo = $PSScriptRoot
if (-not (Test-Path (Join-Path $repo 'new-project.ps1'))) { throw "new-project.ps1 not found at $repo" }
$winPath = $repo -replace '/', '\'

New-Item -ItemType Directory -Force -Path $BinDir | Out-Null

$cmdShim = "@echo off`r`npowershell -NoProfile -ExecutionPolicy Bypass -File `"$winPath\new-project.ps1`" %*`r`n"
$shShim = "#!/bin/sh`nexec powershell -NoProfile -ExecutionPolicy Bypass -File `"$winPath\new-project.ps1`" `"`$@`"`n"

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[IO.File]::WriteAllText((Join-Path $BinDir 'new-project.cmd'), $cmdShim, $utf8NoBom)
[IO.File]::WriteAllText((Join-Path $BinDir 'new-project'), $shShim, $utf8NoBom)

if (-not $NoPath) {
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $entries = @()
    if ($userPath) { $entries = $userPath -split ';' }
    if ($entries -notcontains $BinDir) {
        $newPath = ((@($entries | Where-Object { $_ }) + $BinDir) -join ';')
        [Environment]::SetEnvironmentVariable('Path', $newPath, 'User')
        Write-Host "Added $BinDir to your User PATH. Open a new terminal for it to take effect."
    }
    else {
        Write-Host "$BinDir is already on your User PATH."
    }
}

if (-not $NoConfig) {
    $cfgDir = Join-Path $HOME '.agent-harness'
    $cfgPath = Join-Path $cfgDir 'config.json'
    New-Item -ItemType Directory -Force -Path $cfgDir | Out-Null
    $cfg = if (Test-Path $cfgPath) { Get-Content $cfgPath -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
    if (-not $cfg.repoPath) {
        $cfg | Add-Member -NotePropertyName repoPath -NotePropertyValue $repo -Force
        ($cfg | ConvertTo-Json -Depth 5) | Set-Content -Path $cfgPath -Encoding UTF8
        Write-Host "Set repoPath in $cfgPath so agent skills resolve to this checkout."
    }
}

Write-Host ''
Write-Host "Installed 'new-project'. Try:  new-project   (shows usage)"
Write-Host "Uninstall: remove '$BinDir' from your User PATH and delete the two shims."
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File new-project/tests/install-tests.ps1`
Expected: `install-tests: passed`.

- [ ] **Step 5: Commit**

```bash
git add install.ps1 new-project/tests/install-tests.ps1
git commit -m "Add install.ps1 to put new-project on PATH"
```

---

### Task 4: Refactor the three plugin skills to delegate to the CLI

**Files:**
- Modify: `plugin/skills/new-expo-app/SKILL.md`
- Modify: `plugin/skills/new-dotnet-cli/SKILL.md`
- Modify: `plugin/skills/new-dotnet-etl-api/SKILL.md`

**Interfaces:**
- Consumes: the `new-project.ps1` CLI from Task 2 (resolved via `resolve-repo.ps1`).
- Produces: three thin skills whose bodies call `& "$repo/new-project.ps1" <type> ...` instead of duplicating scaffold/stamp/setup/verify. Frontmatter (`name`, `description`) is preserved so `validate-plugin.ps1` still passes.

- [ ] **Step 1: Rewrite `new-expo-app/SKILL.md`**

Preserve the existing frontmatter block exactly (the `---` ... `name: new-expo-app` ... `description:` ... `---`). Replace the body (everything after the closing `---`) with:

```markdown

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
```

- [ ] **Step 2: Rewrite `new-dotnet-cli/SKILL.md`**

Preserve its frontmatter; replace the body with:

```markdown

# New .NET CLI Project

Scaffold a .NET 10 console solution by delegating to the standalone `new-project` CLI
(the single source of truth for scaffold -> stamp -> setup -> verify).

## Inputs

- **Name** (required): PascalCase solution name, e.g. `MyTool`. Ask if not given.
- **Destination** (optional): defaults to `<cwd>/<Name>`.

## Steps

1. **Resolve the harness repo:**
   ```powershell
   $repo = & "${CLAUDE_PLUGIN_ROOT}/scripts/resolve-repo.ps1" | Select-Object -Last 1
   ```
2. **Run the CLI** (omit `-Destination` when defaulted):
   ```powershell
   & "$repo/new-project.ps1" dotnet-cli <Name> -Destination <dir>
   ```
   This installs the templates, scaffolds, stamps provenance, runs setup, and verifies
   (`dotnet build` + `dotnet test`). If it prints a git-identity error, relay the fix and
   stop until the user configures git, then re-run.
3. **Report** what the CLI printed: the project path, the development lifecycle from the
   project's CLAUDE.md (issue -> feat branch -> commit -> PR), and that analyzers fire at
   error severity.

Never hand-edit `.harness.json` — the CLI writes it.
```

- [ ] **Step 3: Rewrite `new-dotnet-etl-api/SKILL.md`**

Preserve its frontmatter; replace the body with:

```markdown

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
```

- [ ] **Step 4: Verify the plugin still validates**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File plugin/template-tests/validate-plugin.ps1 -RequireFull`
Expected: `Plugin validation passed.` (frontmatter names/descriptions intact; all required skills still present).

- [ ] **Step 5: Commit**

```bash
git add plugin/skills/new-expo-app/SKILL.md plugin/skills/new-dotnet-cli/SKILL.md plugin/skills/new-dotnet-etl-api/SKILL.md
git commit -m "Refactor scaffold skills to delegate to the new-project CLI"
```

---

### Task 5: Full verification pass

**Files:** none (validation only).

- [ ] **Step 1: Run every new test + plugin validation**

Run each and confirm the pass line / exit 0:

```
powershell -NoProfile -ExecutionPolicy Bypass -File new-project/tests/lib-tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File new-project/tests/install-tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File new-project/tests/e2e-dotnet-cli.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File plugin/template-tests/validate-plugin.ps1 -RequireFull
```
Expected: `lib-tests: all passed`, `install-tests: passed`, `e2e-dotnet-cli: passed`, `Plugin validation passed.`

- [ ] **Step 2: Confirm the human-facing surfaces**

```
powershell -NoProfile -ExecutionPolicy Bypass -File new-project.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File new-project.ps1 nope Foo
powershell -NoProfile -ExecutionPolicy Bypass -File new-project.ps1 expo MyApp -BundleId com.acme.myapp -DryRun
```
Expected: (1) usage lists all three types; (2) unknown-type prints the error + usage to stderr and exits 2; (3) prints the resolved plan and exits 0 with no filesystem changes.

- [ ] **Step 3: Commit (only if Steps 1–2 required a fix)**

If a fix was needed:

```bash
git add -A
git commit -m "Fix issues surfaced by the new-project verification pass"
```

Otherwise this task is pure verification — no commit.
