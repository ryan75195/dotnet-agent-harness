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
