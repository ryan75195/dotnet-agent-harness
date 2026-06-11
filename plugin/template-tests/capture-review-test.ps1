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
Assert (@($events).Count -eq 3) "expected 3 events (bot, approval, pr-author excluded), got $(@($events).Count)"
foreach ($e in $events) { Assert ($e.kind -eq 'review-comment') 'kind is review-comment' }
$ids = ($events | ForEach-Object { $_.id } | Sort-Object) -join ','
Assert ($ids -eq 'rc-1001,rc-1002,rc-1003') "ids must be rc-1001..1003, got $ids"
$e1 = $events | Where-Object { $_.id -eq 'rc-1001' }
Assert ($e1.file -eq 'src/features/x/X.tsx' -and $e1.line -eq 17) 'inline file/line captured'
Assert ($e1.template -eq 'expo-app') 'template from stamp'
Assert ($e1.pr -eq 42) 'pr captured'
Assert (Test-Path (Join-Path $fb 'diffs/rc-1001.hunk.patch')) 'hunk sidecar written for inline comment'

& (Join-Path $pluginScripts 'capture-review.ps1') -InputJson $inputPath -ProjectDir $proj -Pr 42 -PrUrl 'https://gh/x/pull/42'
$events2 = Get-Content (Join-Path $fb 'events.jsonl') | ForEach-Object { $_ | ConvertFrom-Json }
Assert (@($events2).Count -eq 3) "re-run must not duplicate, still 3, got $(@($events2).Count)"

Remove-Item Env:AGENT_HARNESS_FEEDBACK_DIR
Write-Host 'Capture-review tests passed.'
Remove-Item -Recurse -Force $work
