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
