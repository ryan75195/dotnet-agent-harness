param(
    [string]$InputJson,
    [string]$ProjectDir = (Get-Location).Path,
    [Parameter(Mandatory = $true)][int]$Pr,
    [Parameter(Mandatory = $true)][string]$PrUrl
)

$ErrorActionPreference = 'Stop'

if ($InputJson) { $raw = Get-Content $InputJson -Raw } else { $raw = [Console]::In.ReadToEnd() }
$parsed = $raw | ConvertFrom-Json
$comments = @($parsed)

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
