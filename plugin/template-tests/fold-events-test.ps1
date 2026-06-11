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
