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
