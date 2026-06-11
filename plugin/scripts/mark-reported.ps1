param(
    [Parameter(Mandatory = $true)][string]$EventId,
    [Parameter(Mandatory = $true)][string]$IssueUrl
)

$ErrorActionPreference = 'Stop'

$feedbackDir = if ($env:AGENT_HARNESS_FEEDBACK_DIR) { $env:AGENT_HARNESS_FEEDBACK_DIR } else { Join-Path $HOME '.agent-harness/feedback' }
$store = Join-Path $feedbackDir 'events.jsonl'
if (-not (Test-Path $store)) { throw "Feedback store not found: $store" }

$line = [pscustomobject]@{ id = $EventId; reportedIssue = $IssueUrl } | ConvertTo-Json -Compress
Add-Content -Path $store -Value $line
Write-Output "Marked event $EventId as reported in $IssueUrl."
