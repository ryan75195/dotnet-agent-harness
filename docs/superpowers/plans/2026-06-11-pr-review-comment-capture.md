# PR Review-Comment Capture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Capture human PR review comments into the agent-harness feedback store as `kind: review-comment` events, feeding `harness-report`'s rule/arch-test proposals.

**Architecture:** A `kind` field is added to the event schema (absent = `gate-failure`). A testable `capture-review.ps1` turns `gh`-shaped PR-comment JSON into events (human-only, deduped on GitHub comment id); a thin `harness-capture-review` skill runs `gh` and pipes to it, routed from the template merge step. A `fold-events.ps1` helper folds the append-only store by id (defaulting `kind`), used by `harness-report`.

**Tech Stack:** Windows PowerShell 5.1-compatible scripts, bash (Git for Windows) hooks, plain-PowerShell tests, GitHub Actions.

---

## Conventions for the executor (read first)

- **Working directory:** repo root `C:\Users\ryan7\programming\dotnet-agent-harness`. This repo commits directly to `main` (its established practice) — do NOT branch.
- **PowerShell scripts must run on Windows PowerShell 5.1**: no `&&`, no ternary, no `?.`, 2-arg `Join-Path` only (forward slashes inside the child path are fine).
- **Do not redirect native `git`/`gh` stderr with `2>&1`** under `$ErrorActionPreference='Stop'` — it raises NativeCommandError on PS 5.1. Use `2>$null` inside `try { } catch { }` and check `$LASTEXITCODE`, as the existing `get-update.ps1`/`resolve-repo.ps1` do.
- **`renames` in `.harness.json` is an ordered array of `{from,to}` pairs** (not relevant here, but don't be surprised).
- **`.gitattributes` already forces LF** on `*.sh` and `.githooks/*`, so copied bash files stay LF.
- Build per-event JSON with `[pscustomobject]@{...} | ConvertTo-Json -Compress` (correct escaping of quotes/newlines), the pattern `harness-note.ps1` uses — never hand-escape.
- Tests are plain PowerShell that `throw` on failure (mirroring `plugin/template-tests/*.ps1`). Run from repo root.
- Commit messages: imperative, no conventional-commit prefix, ending with a blank line then `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

## File structure (created/modified)

```
expo/templates/app/.githooks/harness-feedback.sh        MOD  add "kind":"gate-failure"
dotnet/templates/cli/.githooks/harness-feedback.sh      MOD  byte-copy of expo's
dotnet/templates/etl-api/.githooks/harness-feedback.sh  MOD  byte-copy of expo's
plugin/template-tests/feedback-kind-test.ps1            NEW  asserts kind + 3 copies identical
plugin/scripts/fold-events.ps1                          NEW  fold store by id, default kind
plugin/template-tests/fold-events-test.ps1              NEW
plugin/scripts/capture-review.ps1                       NEW  gh-JSON -> review-comment events
plugin/template-tests/capture-review-test.ps1           NEW
plugin/skills/harness-capture-review/SKILL.md           NEW
plugin/template-tests/validate-plugin.ps1               MOD  -RequireFull lists
expo/templates/app/CLAUDE.md                            MOD  merge-step routing line
dotnet/templates/cli/CLAUDE.md                          MOD  merge-step routing line
dotnet/templates/etl-api/CLAUDE.md                      MOD  merge-step routing line
plugin/skills/harness-report/SKILL.md                   MOD  fold via helper, cluster by kind, remedy
.github/workflows/template-ci.yml                       MOD  run new tests
```

---

### Task 1: `kind` field on gate-failure events

**Files:**
- Modify: `expo/templates/app/.githooks/harness-feedback.sh`
- Modify (by copy): `dotnet/templates/cli/.githooks/harness-feedback.sh`, `dotnet/templates/etl-api/.githooks/harness-feedback.sh`
- Create: `plugin/template-tests/feedback-kind-test.ps1`

- [ ] **Step 1: Write the failing test**

Create `plugin/template-tests/feedback-kind-test.ps1`:

```powershell
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$gitBash = Join-Path (Split-Path (Split-Path (Get-Command git).Source)) 'bin\bash.exe'
if (-not (Test-Path $gitBash)) { $gitBash = 'bash' }

function Assert([bool]$Cond, [string]$Msg) { if (-not $Cond) { throw "ASSERT FAILED: $Msg" } }

$fb = Join-Path $repoRoot 'expo/templates/app/.githooks/harness-feedback.sh'
$cli = Join-Path $repoRoot 'dotnet/templates/cli/.githooks/harness-feedback.sh'
$etl = Join-Path $repoRoot 'dotnet/templates/etl-api/.githooks/harness-feedback.sh'
$h = (Get-FileHash $fb -Algorithm SHA256).Hash
Assert ((Get-FileHash $cli -Algorithm SHA256).Hash -eq $h) 'cli harness-feedback.sh must match expo'
Assert ((Get-FileHash $etl -Algorithm SHA256).Hash -eq $h) 'etl-api harness-feedback.sh must match expo'

$work = Join-Path $env:TEMP 'agent-harness-kind-test'
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory (Join-Path $work '.githooks') -Force | Out-Null
$fbDir = Join-Path $work 'fb'
Copy-Item $fb (Join-Path $work '.githooks/harness-feedback.sh')
git -C $work init -q -b main
git -C $work config user.email t@t.local
git -C $work config user.name t
Set-Content (Join-Path $work '.harness.json') '{ "template": "expo-app" }'
$env:AGENT_HARNESS_FEEDBACK_DIR = $fbDir.Replace([char]92, [char]47)
$workFwd = $work.Replace([char]92, [char]47)
& $gitBash -c "cd '$workFwd' && . .githooks/harness-feedback.sh && harness_log_failure lint 'seeded output'" | Out-Null
Remove-Item Env:AGENT_HARNESS_FEEDBACK_DIR
$evt = Get-Content (Join-Path $fbDir 'events.jsonl') -Raw | ConvertFrom-Json
Assert ($evt.kind -eq 'gate-failure') "event kind must be gate-failure, got '$($evt.kind)'"
Assert ($evt.gate -eq 'lint') 'gate preserved'

Write-Host 'Feedback-kind tests passed.'
Remove-Item -Recurse -Force $work
```

- [ ] **Step 2: Run to verify it fails**

Run: `./plugin/template-tests/feedback-kind-test.ps1`
Expected: FAIL — `event kind must be gate-failure, got ''` (the field isn't emitted yet).

- [ ] **Step 3: Add the kind field to the expo harness-feedback.sh printf**

In `expo/templates/app/.githooks/harness-feedback.sh`, change the printf format string and nothing else. Old (the `printf '{...}\n'` line and its args):

```bash
    printf '{"id":"%s","ts":"%s","project":"%s","template":"%s","gate":"%s","branch":"%s","outputTail":"%s","note":null,"fixCommit":null}\n' \
      "$id" "$ts" "$project" "$template" "$gate" "$branch" "$escaped" \
```

New (insert `"kind":"gate-failure",` right after the id field; args unchanged):

```bash
    printf '{"id":"%s","kind":"gate-failure","ts":"%s","project":"%s","template":"%s","gate":"%s","branch":"%s","outputTail":"%s","note":null,"fixCommit":null}\n' \
      "$id" "$ts" "$project" "$template" "$gate" "$branch" "$escaped" \
```

- [ ] **Step 4: Copy the edited file to both dotnet templates (keep byte-identical)**

```powershell
Copy-Item expo/templates/app/.githooks/harness-feedback.sh dotnet/templates/cli/.githooks/harness-feedback.sh
Copy-Item expo/templates/app/.githooks/harness-feedback.sh dotnet/templates/etl-api/.githooks/harness-feedback.sh
```

- [ ] **Step 5: Run to verify it passes**

Run: `./plugin/template-tests/feedback-kind-test.ps1`
Expected: `Feedback-kind tests passed.`

- [ ] **Step 6: Commit**

```powershell
git add expo/templates/app/.githooks/harness-feedback.sh dotnet/templates/cli/.githooks/harness-feedback.sh dotnet/templates/etl-api/.githooks/harness-feedback.sh plugin/template-tests/feedback-kind-test.ps1
git commit -m "Tag gate-failure feedback events with kind field"
```

---

### Task 2: `fold-events.ps1` — fold the append-only store by id

**Files:**
- Create: `plugin/scripts/fold-events.ps1`
- Create: `plugin/template-tests/fold-events-test.ps1`

The store is append-only: a later line with the same `id` patches earlier fields (note, fixCommit, reportedIssue). This helper produces current-state-per-id and defaults a missing `kind` to `gate-failure` (back-compat for events captured before Task 1).

- [ ] **Step 1: Write the failing test**

Create `plugin/template-tests/fold-events-test.ps1`:

```powershell
$ErrorActionPreference = 'Stop'

$pluginScripts = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts'
$work = Join-Path $env:TEMP 'agent-harness-fold-test'
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory $work | Out-Null

function Assert([bool]$Cond, [string]$Msg) { if (-not $Cond) { throw "ASSERT FAILED: $Msg" } }

$store = Join-Path $work 'events.jsonl'
Set-Content $store '{"id":"aa11","ts":"t1","gate":"lint","note":null,"fixCommit":null}'
Add-Content $store '{"id":"aa11","note":"adding paywall"}'
Add-Content $store '{"id":"aa11","fixCommit":"abc123"}'
Add-Content $store '{"id":"rc-99","kind":"review-comment","author":"alice","body":"use lib"}'

$folded = & (Join-Path $pluginScripts 'fold-events.ps1') -StorePath $store | Out-String | ConvertFrom-Json
$byId = @{}
foreach ($e in $folded) { $byId[$e.id] = $e }

Assert ($folded.Count -eq 2) "expected 2 folded events, got $($folded.Count)"
Assert ($byId['aa11'].kind -eq 'gate-failure') 'missing kind defaults to gate-failure'
Assert ($byId['aa11'].note -eq 'adding paywall') 'note patch folded in'
Assert ($byId['aa11'].fixCommit -eq 'abc123') 'fixCommit patch folded in'
Assert ($byId['rc-99'].kind -eq 'review-comment') 'explicit kind preserved'
Assert ($byId['rc-99'].body -eq 'use lib') 'review-comment fields preserved'

Write-Host 'Fold-events tests passed.'
Remove-Item -Recurse -Force $work
```

- [ ] **Step 2: Run to verify it fails**

Run: `./plugin/template-tests/fold-events-test.ps1`
Expected: FAIL — `fold-events.ps1` not found.

- [ ] **Step 3: Write fold-events.ps1**

Create `plugin/scripts/fold-events.ps1`:

```powershell
param(
    [string]$StorePath
)

$ErrorActionPreference = 'Stop'

if (-not $StorePath) {
    $feedbackDir = if ($env:AGENT_HARNESS_FEEDBACK_DIR) { $env:AGENT_HARNESS_FEEDBACK_DIR } else { Join-Path $HOME '.agent-harness/feedback' }
    $StorePath = Join-Path $feedbackDir 'events.jsonl'
}
if (-not (Test-Path $StorePath)) { Write-Output '[]'; exit 0 }

$order = New-Object System.Collections.ArrayList
$byId = @{}
foreach ($line in Get-Content $StorePath) {
    if (-not $line.Trim()) { continue }
    $obj = $null
    try { $obj = $line | ConvertFrom-Json } catch { continue }
    if (-not $obj.id) { continue }
    if (-not $byId.ContainsKey($obj.id)) {
        $acc = [ordered]@{}
        $byId[$obj.id] = $acc
        [void]$order.Add($obj.id)
    }
    $acc = $byId[$obj.id]
    foreach ($p in $obj.PSObject.Properties) { $acc[$p.Name] = $p.Value }
}

$result = @()
foreach ($id in $order) {
    $acc = $byId[$id]
    if (-not $acc.Contains('kind')) { $acc['kind'] = 'gate-failure' }
    $result += [pscustomobject]$acc
}

$result | ConvertTo-Json -Depth 6
```

Note: `ConvertTo-Json` on a single-element array emits an object, not an array; the test pipes through `Out-String | ConvertFrom-Json` and uses `.Count`, which is `1` for a single object and works either way. For multi-element (this test has 2) it emits an array. Do not add `-AsArray` (unavailable in PS 5.1).

- [ ] **Step 4: Run to verify it passes**

Run: `./plugin/template-tests/fold-events-test.ps1`
Expected: `Fold-events tests passed.`

- [ ] **Step 5: Commit**

```powershell
git add plugin/scripts/fold-events.ps1 plugin/template-tests/fold-events-test.ps1
git commit -m "Add fold-events helper defaulting kind to gate-failure"
```

---

### Task 3: `capture-review.ps1` — gh JSON to review-comment events

**Files:**
- Create: `plugin/scripts/capture-review.ps1`
- Create: `plugin/template-tests/capture-review-test.ps1`

**Input contract** (the normalized array the skill produces from `gh`): each element has `commentId` (number), `authorType` (`User`/`Bot`/...), `author` (login), `isPrAuthor` (bool), `body` (string), `file` (string|null), `line` (number|null), `diffHunk` (string|null), `commentUrl` (string).

- [ ] **Step 1: Write the failing test**

Create `plugin/template-tests/capture-review-test.ps1`:

```powershell
$ErrorActionPreference = 'Stop'

$pluginScripts = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts'
$work = Join-Path $env:TEMP 'agent-harness-capture-review-test'
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory $work | Out-Null

function Assert([bool]$Cond, [string]$Msg) { if (-not $Cond) { throw "ASSERT FAILED: $Msg" } }

$proj = Join-Path $work 'proj'
New-Item -ItemType Directory $proj | Out-Null
Set-Content (Join-Path $proj '.harness.json') '{ "template": "expo-app", "stack": "expo" }'
$fb = Join-Path $work 'fb'
$env:AGENT_HARNESS_FEEDBACK_DIR = $fb.Replace([char]92, [char]47)

$fixture = @(
    [pscustomobject]@{ commentId = 1001; authorType = 'User'; author = 'rev'; isPrAuthor = $false; body = 'feature imports a sibling feature - route through lib'; file = 'src/features/x/X.tsx'; line = 17; diffHunk = '@@ -1 +1 @@'; commentUrl = 'https://gh/x#r1001' },
    [pscustomobject]@{ commentId = 1002; authorType = 'User'; author = 'rev'; isPrAuthor = $false; body = 'name this by intent, not implementation'; file = 'src/lib/y.ts'; line = 4; diffHunk = '@@ -4 +4 @@'; commentUrl = 'https://gh/x#r1002' },
    [pscustomobject]@{ commentId = 1003; authorType = 'User'; author = 'rev'; isPrAuthor = $false; body = 'Overall the layering here drifts from lib<-features'; file = $null; line = $null; diffHunk = $null; commentUrl = 'https://gh/x#r1003' },
    [pscustomobject]@{ commentId = 1004; authorType = 'Bot'; author = 'code-review[bot]'; isPrAuthor = $false; body = 'nit: prefer const'; file = 'src/a.ts'; line = 2; diffHunk = '@@'; commentUrl = 'https://gh/x#r1004' },
    [pscustomobject]@{ commentId = 1005; authorType = 'User'; author = 'rev'; isPrAuthor = $false; body = 'LGTM'; file = $null; line = $null; diffHunk = $null; commentUrl = 'https://gh/x#r1005' },
    [pscustomobject]@{ commentId = 1006; authorType = 'User'; author = 'me'; isPrAuthor = $true; body = 'I will fix the layering'; file = $null; line = $null; diffHunk = $null; commentUrl = 'https://gh/x#r1006' }
)
$inputPath = Join-Path $work 'input.json'
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[IO.File]::WriteAllText($inputPath, ($fixture | ConvertTo-Json -Depth 6), $utf8NoBom)

& (Join-Path $pluginScripts 'capture-review.ps1') -InputJson $inputPath -ProjectDir $proj -Pr 42 -PrUrl 'https://gh/x/pull/42'

$lines = Get-Content (Join-Path $fb 'events.jsonl')
$events = $lines | ForEach-Object { $_ | ConvertFrom-Json }
Assert ($events.Count -eq 3) "expected 3 events (bot, approval, pr-author excluded), got $($events.Count)"
foreach ($e in $events) { Assert ($e.kind -eq 'review-comment') 'kind is review-comment' }
$ids = $events | ForEach-Object { $_.id } | Sort-Object
Assert ($ids -join ',' -eq 'rc-1001,rc-1002,rc-1003') "ids must be rc-1001..1003, got $($ids -join ',')"
$e1 = $events | Where-Object { $_.id -eq 'rc-1001' }
Assert ($e1.file -eq 'src/features/x/X.tsx' -and $e1.line -eq 17) 'inline file/line captured'
Assert ($e1.template -eq 'expo-app') 'template from stamp'
Assert ($e1.pr -eq 42) 'pr captured'
Assert (Test-Path (Join-Path $fb 'diffs/rc-1001.hunk.patch')) 'hunk sidecar written for inline comment'

& (Join-Path $pluginScripts 'capture-review.ps1') -InputJson $inputPath -ProjectDir $proj -Pr 42 -PrUrl 'https://gh/x/pull/42'
$events2 = Get-Content (Join-Path $fb 'events.jsonl') | ForEach-Object { $_ | ConvertFrom-Json }
Assert ($events2.Count -eq 3) "re-run must not duplicate, still 3, got $($events2.Count)"

Remove-Item Env:AGENT_HARNESS_FEEDBACK_DIR
Write-Host 'Capture-review tests passed.'
Remove-Item -Recurse -Force $work
```

- [ ] **Step 2: Run to verify it fails**

Run: `./plugin/template-tests/capture-review-test.ps1`
Expected: FAIL — `capture-review.ps1` not found.

- [ ] **Step 3: Write capture-review.ps1**

Create `plugin/scripts/capture-review.ps1`:

```powershell
param(
    [string]$InputJson,
    [string]$ProjectDir = (Get-Location).Path,
    [Parameter(Mandatory = $true)][int]$Pr,
    [Parameter(Mandatory = $true)][string]$PrUrl
)

$ErrorActionPreference = 'Stop'

if ($InputJson) { $raw = Get-Content $InputJson -Raw } else { $raw = [Console]::In.ReadToEnd() }
$comments = @($raw | ConvertFrom-Json)

$feedbackDir = if ($env:AGENT_HARNESS_FEEDBACK_DIR) { $env:AGENT_HARNESS_FEEDBACK_DIR } else { Join-Path $HOME '.agent-harness/feedback' }
$diffsDir = Join-Path $feedbackDir 'diffs'
if (-not (Test-Path $diffsDir)) { New-Item -ItemType Directory -Force $diffsDir | Out-Null }
$store = Join-Path $feedbackDir 'events.jsonl'

$template = ''
$stampPath = Join-Path $ProjectDir '.harness.json'
if (Test-Path $stampPath) { $template = (Get-Content $stampPath -Raw | ConvertFrom-Json).template }

$existing = @{}
if (Test-Path $store) {
    foreach ($line in Get-Content $store) {
        if (-not $line.Trim()) { continue }
        try { $o = $line | ConvertFrom-Json } catch { continue }
        if ($o.id) { $existing[$o.id] = $true }
    }
}

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$captured = 0
$skipped = 0
foreach ($c in $comments) {
    $id = "rc-$($c.commentId)"
    $bodyTrim = ''
    if ($null -ne $c.body) { $bodyTrim = ([string]$c.body).Trim() }
    $isApproval = $bodyTrim -match '^(LGTM|:\+1:|approved|ship it|done|thanks|ty)\.?$'
    if ($c.authorType -ne 'User' -or $c.isPrAuthor -eq $true -or -not $bodyTrim -or $isApproval -or $existing.ContainsKey($id)) {
        $skipped++
        continue
    }
    $ts = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    $event = [pscustomobject]@{
        id            = $id
        kind          = 'review-comment'
        ts            = $ts
        project       = $ProjectDir
        template      = $template
        pr            = $Pr
        prUrl         = $PrUrl
        commentUrl    = $c.commentUrl
        commentId     = $c.commentId
        author        = $c.author
        file          = $c.file
        line          = $c.line
        body          = $c.body
        note          = $null
        reportedIssue = $null
    }
    Add-Content -Path $store -Value ($event | ConvertTo-Json -Compress)
    if ($c.diffHunk) {
        [IO.File]::WriteAllText((Join-Path $diffsDir "$id.hunk.patch"), [string]$c.diffHunk, $utf8NoBom)
    }
    $existing[$id] = $true
    $captured++
}

Write-Output "Captured $captured review comment(s) from PR $Pr ($skipped skipped)."
```

- [ ] **Step 4: Run to verify it passes**

Run: `./plugin/template-tests/capture-review-test.ps1`
Expected: `Capture-review tests passed.`

- [ ] **Step 5: Commit**

```powershell
git add plugin/scripts/capture-review.ps1 plugin/template-tests/capture-review-test.ps1
git commit -m "Add capture-review script writing review-comment events"
```

---

### Task 4: `harness-capture-review` skill + validation

**Files:**
- Create: `plugin/skills/harness-capture-review/SKILL.md`
- Modify: `plugin/template-tests/validate-plugin.ps1`

- [ ] **Step 1: Create the skill**

Create `plugin/skills/harness-capture-review/SKILL.md`:

```markdown
---
name: harness-capture-review
description: Use after a PR merges, or on request, to capture human review comments from a PR into the harness feedback store ("capture the review comments", "harvest PR feedback"). Routed from the merge step.
---

# Harness Capture Review

Capture **human** PR review comments into the feedback store as
`review-comment` events, so `harness-report` can later turn recurring
review feedback into new guardrails. Bots are skipped; capture is
idempotent (deduped on the GitHub comment id) and never blocks the merge
that preceded it.

## Flow

1. **Determine the PR.** Use the explicit PR number if given, else the PR
   for the current branch:
   ```powershell
   gh pr view --json number,url,author
   ```
   Resolve `owner/repo` with `gh repo view --json nameWithOwner`.

2. **Fetch human review data** (two sources):
   ```powershell
   gh api "repos/<owner>/<repo>/pulls/<pr>/comments" --paginate
   gh api "repos/<owner>/<repo>/pulls/<pr>/reviews" --paginate
   ```
   The first is inline comments (fields `id`, `user.login`, `user.type`,
   `path`, `line`, `body`, `diff_hunk`, `html_url`). The second is review
   summaries (use entries with a non-empty `body`; `user`, `state`,
   `html_url`).

3. **Normalize** both into one array where each element has: `commentId`
   (the numeric `id`), `authorType` (`user.type`), `author` (`user.login`),
   `isPrAuthor` (`true` when `user.login` equals the PR author), `body`,
   `file` (`path` or null for summaries), `line` (or null), `diffHunk`
   (`diff_hunk` or null), `commentUrl` (`html_url`). Write it to a temp
   JSON file.

4. **Capture:**
   ```powershell
   & "${CLAUDE_PLUGIN_ROOT}/scripts/capture-review.ps1" -InputJson <temp> -Pr <pr> -PrUrl <url>
   ```

5. **Report** the one-line summary the script prints. If any `gh` call
   fails (no PR, no auth, offline), say so and stop — do not retry in a
   loop and do not treat it as fatal to the surrounding work.
```

- [ ] **Step 2: Add the skill and script to validate-plugin -RequireFull**

In `plugin/template-tests/validate-plugin.ps1`, update the two lists inside the `if ($RequireFull)` block.

Old:
```powershell
    $required = @('new-dotnet-cli', 'new-dotnet-etl-api', 'new-expo-app', 'harness-update', 'harness-report')
```
New:
```powershell
    $required = @('new-dotnet-cli', 'new-dotnet-etl-api', 'new-expo-app', 'harness-update', 'harness-report', 'harness-capture-review')
```

Old:
```powershell
    foreach ($script in @('resolve-repo.ps1', 'write-stamp.ps1', 'harness-note.ps1', 'mark-reported.ps1', 'get-update.ps1', 'apply-update.ps1')) {
```
New:
```powershell
    foreach ($script in @('resolve-repo.ps1', 'write-stamp.ps1', 'harness-note.ps1', 'mark-reported.ps1', 'get-update.ps1', 'apply-update.ps1', 'fold-events.ps1', 'capture-review.ps1')) {
```

- [ ] **Step 3: Run validation**

Run: `./plugin/template-tests/validate-plugin.ps1 -RequireFull`
Expected: `Plugin validation passed.`

- [ ] **Step 4: Commit**

```powershell
git add plugin/skills/harness-capture-review plugin/template-tests/validate-plugin.ps1
git commit -m "Add harness-capture-review skill and require it in validation"
```

---

### Task 5: Merge-step routing in template CLAUDE.md files

**Files:**
- Modify: `expo/templates/app/CLAUDE.md`
- Modify: `dotnet/templates/cli/CLAUDE.md`
- Modify: `dotnet/templates/etl-api/CLAUDE.md`

Each template's "Development lifecycle" has a squash-merge step. Add a capture follow-on. The exact merge line differs per template, so anchor on each.

- [ ] **Step 1: Expo — add capture after the squash-merge step**

In `expo/templates/app/CLAUDE.md`, find:
```markdown
6. **Squash merge.** `gh pr merge <N> --squash --delete-branch`.
```
Replace with:
```markdown
6. **Squash merge.** `gh pr merge <N> --squash --delete-branch`.
7. **Capture review feedback.** After the merge, invoke the
   `agent-harness:harness-capture-review` skill for PR `<N>` to record any
   human review comments for later rule synthesis (best-effort; never
   blocks).
```

- [ ] **Step 2: cli — same**

In `dotnet/templates/cli/CLAUDE.md`, find:
```markdown
6. **Squash merge.** `gh pr merge <N> --squash --delete-branch`.
```
Replace with the same 6+7 block as Step 1.

- [ ] **Step 3: etl-api — same**

In `dotnet/templates/etl-api/CLAUDE.md`, find:
```markdown
6. **Squash merge.** `gh pr merge <N> --squash --delete-branch`.
```
Replace with the same 6+7 block as Step 1.

- [ ] **Step 4: Verify all three updated**

Run: `Select-String -Path expo/templates/app/CLAUDE.md,dotnet/templates/cli/CLAUDE.md,dotnet/templates/etl-api/CLAUDE.md -Pattern 'harness-capture-review' | Measure-Object | Select-Object -ExpandProperty Count`
Expected: `3`

If any file's merge line differs from the anchor text above, open that file, locate the squash-merge lifecycle step, and add the identical step 7 after it.

- [ ] **Step 5: Commit**

```powershell
git add expo/templates/app/CLAUDE.md dotnet/templates/cli/CLAUDE.md dotnet/templates/etl-api/CLAUDE.md
git commit -m "Route the merge step to review-comment capture in templates"
```

---

### Task 6: `harness-report` consumes the new kind

**Files:**
- Modify: `plugin/skills/harness-report/SKILL.md`

- [ ] **Step 1: Update the skill to fold via the helper and cluster by kind**

In `plugin/skills/harness-report/SKILL.md`, replace the `### 1. Load the store` and `### 2. Cluster` sections with:

```markdown
### 1. Load the store

Fold the append-only store to current-state-per-id with the helper (it
defaults a missing `kind` to `gate-failure` for back-compat):
```powershell
& "${CLAUDE_PLUGIN_ROOT}/scripts/fold-events.ps1" | ConvertFrom-Json
```
Skip events that already have `reportedIssue`. If nothing is unreported,
say so and stop.

### 2. Cluster (within a kind, never across)

Group unreported events by recurring mistake, clustering **within a single
`kind`** — never mix `gate-failure` and `review-comment` in one cluster.

- `gate-failure`: same gate + template + similar failure text/note. Read
  the `diffs/<id>.failure.patch` / `.fix.patch` sidecars when the
  outputTail is not enough.
- `review-comment`: same template + similar guidance across PRs/files. Read
  the `diffs/<id>.hunk.patch` sidecar for the code the comment was on.
  These are the highest-value clusters — a comment a human reviewer had to
  leave is a rule the toolchain did not enforce.

For each `review-comment` cluster, propose a **remedy type** from the
comment bodies and file paths:
- layering/structure ("imports a sibling", "belongs in lib/service",
  dependency direction) → an arch-test fixture (.NET
  `tests/*.Architecture`) or a dependency-cruiser rule (expo);
- naming/style ("no any", "name by intent", formatting) → an analyzer
  (CI####) or an ESLint rule;
- otherwise → CLAUDE.md guidance.

Singletons are normally not worth reporting — mention them and let the user
decide.
```

- [ ] **Step 2: Note the new kind in the draft section**

In `plugin/skills/harness-report/SKILL.md`, in the `### 3. Draft and confirm` section, find the sentence beginning `For each cluster, draft an issue body containing ONLY:` and append to that paragraph:

```markdown
For `review-comment` clusters, also include the proposed remedy type and
the reviewer guidance summarized in your own words — never paste raw diff
hunks (sidecars stay local; the harness repo is public).
```

- [ ] **Step 3: Validate the skill still parses**

Run: `./plugin/template-tests/validate-plugin.ps1 -RequireFull`
Expected: `Plugin validation passed.`

- [ ] **Step 4: Commit**

```powershell
git add plugin/skills/harness-report/SKILL.md
git commit -m "Teach harness-report to fold by kind and propose review-comment remedies"
```

---

### Task 7: CI wiring

**Files:**
- Modify: `.github/workflows/template-ci.yml`

- [ ] **Step 1: Add the new test scripts to the plugin-validate job**

In `.github/workflows/template-ci.yml`, find the end of the `plugin-validate` job (after the `Update-script tests` step):
```yaml
      - name: Update-script tests
        shell: pwsh
        run: ./plugin/template-tests/update-scripts-test.ps1
```
Append:
```yaml

      - name: Feedback-kind tests
        shell: pwsh
        run: ./plugin/template-tests/feedback-kind-test.ps1

      - name: Fold-events tests
        shell: pwsh
        run: ./plugin/template-tests/fold-events-test.ps1

      - name: Capture-review tests
        shell: pwsh
        run: ./plugin/template-tests/capture-review-test.ps1
```

- [ ] **Step 2: Final local sweep**

```powershell
./plugin/template-tests/validate-plugin.ps1 -RequireFull
./plugin/template-tests/feedback-kind-test.ps1
./plugin/template-tests/fold-events-test.ps1
./plugin/template-tests/capture-review-test.ps1
./plugin/template-tests/annotate-test.ps1
./plugin/template-tests/stamp-test.ps1
./plugin/template-tests/update-scripts-test.ps1
```
Expected: all pass.

- [ ] **Step 3: Commit**

```powershell
git add .github/workflows/template-ci.yml
git commit -m "Run review-capture tests in plugin CI job"
```

---

## Post-implementation verification (manual, once)

Not part of CI: in a real repo with a merged PR that has human review
comments, run the `harness-capture-review` skill against that PR and
confirm `review-comment` events appear in `~/.agent-harness/feedback/` with
correct file/line, bots excluded, and a re-run adds nothing.
