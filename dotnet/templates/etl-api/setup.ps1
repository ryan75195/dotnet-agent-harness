$ErrorActionPreference = 'Stop'

function Write-FactoryPolicy {
    param([string]$ProjectDir)

    $policyPath = Join-Path $ProjectDir '.github/agent-factory.yml'
    if (Test-Path -LiteralPath $policyPath -PathType Leaf) {
        return
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

    $policyDir = Split-Path -Parent $policyPath
    New-Item -ItemType Directory -Path $policyDir -Force | Out-Null
    $policyContent = @"
version: 1
enabled: true
defaultProvider: opencode
baseBranch: main
authorizedMaintainers: [$login]
concurrencyLimit: 2
retry:
  maxAttempts: 3
  backoffSeconds: 60
"@
    $policyContent = $policyContent.TrimEnd("`r", "`n") + "`n"
    $utf8NoBom = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText($policyPath, $policyContent, $utf8NoBom)

    if ($usedPlaceholder) {
        Write-Host "NOTE: could not determine your GitHub login (gh missing or not authenticated)." -ForegroundColor Yellow
        Write-Host "  Edit .github/agent-factory.yml and replace REPLACE_WITH_YOUR_GITHUB_LOGIN with your GitHub username before the first factory ticket."
    }
}

Write-FactoryPolicy -ProjectDir (Get-Location).Path

function Invoke-Git {
    & git @Args
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Args -join ' ') failed with exit code $LASTEXITCODE"
    }
}

$gitEmail = git config user.email 2>$null
$gitName = git config user.name 2>$null
if (-not $gitEmail -or -not $gitName) {
    Write-Host "ERROR: git identity is not configured." -ForegroundColor Red
    Write-Host ""
    Write-Host "setup.ps1 needs git user.email and user.name for the initial commit."
    Write-Host "Configure them once globally:"
    Write-Host ""
    Write-Host "  git config --global user.email 'your@email.com'"
    Write-Host "  git config --global user.name 'Your Name'"
    Write-Host ""
    Write-Host "Then re-run setup.ps1."
    exit 1
}

Write-Host "Initializing git repo..."
Invoke-Git init -q -b main
Invoke-Git add .

Write-Host "Activating .githooks..."
Invoke-Git config core.hooksPath .githooks

Write-Host "Creating initial commit..."
Invoke-Git commit -q --no-verify -m "Initial scaffold from etl-api template"

Write-Host ""
Write-Host "Done. Next steps:"
Write-Host "  1. dotnet restore; dotnet build"
Write-Host "  2. gh repo create"
Write-Host "  3. ./scripts/factory-labels.ps1"
Write-Host "  4. gh issue create --title '...'"
