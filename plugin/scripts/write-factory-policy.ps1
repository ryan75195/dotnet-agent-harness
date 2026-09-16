param(
    [Parameter(Mandatory = $true)][string]$ProjectDir
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$templatePath = Join-Path $repoRoot 'templates/agent-factory.yml'
$policyPath = Join-Path $ProjectDir '.github/agent-factory.yml'

if (Test-Path -LiteralPath $policyPath -PathType Leaf) {
    Write-Output 'agent-factory.yml already exists, leaving it unchanged.'
    exit 0
}

$login = $null
try {
    $ghOutput = & gh api user --jq .login 2>$null
    if ($LASTEXITCODE -eq 0 -and $ghOutput) {
        $login = ($ghOutput | Out-String).Trim()
    }
}
catch {
    $login = $null
}
$global:LASTEXITCODE = 0

$usedPlaceholder = $false
if ([string]::IsNullOrWhiteSpace($login)) {
    $login = 'REPLACE_WITH_YOUR_GITHUB_LOGIN'
    $usedPlaceholder = $true
}

$template = [IO.File]::ReadAllText($templatePath)
$content = $template.Replace('__LOGIN__', $login)
$content = $content.TrimEnd("`r", "`n") + "`n"

$policyDir = Split-Path -Parent $policyPath
New-Item -ItemType Directory -Path $policyDir -Force | Out-Null
$utf8NoBom = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($policyPath, $content, $utf8NoBom)

if ($usedPlaceholder) {
    Write-Output 'Could not determine your GitHub login (gh is missing or not authenticated).'
    Write-Output 'Edit .github/agent-factory.yml and replace REPLACE_WITH_YOUR_GITHUB_LOGIN with your GitHub username before the first factory ticket.'
}
else {
    Write-Output "Wrote .github/agent-factory.yml (authorizedMaintainers: [$login])."
}
