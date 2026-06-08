$ErrorActionPreference = 'Stop'

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
    Write-Host "Configure it once globally, then re-run setup.ps1:"
    Write-Host "  git config --global user.email 'your@email.com'"
    Write-Host "  git config --global user.name 'Your Name'"
    exit 1
}

Write-Host "Installing dependencies..."
npm install
if ($LASTEXITCODE -ne 0) {
    throw "npm install failed"
}

Write-Host "Initializing git repo..."
Invoke-Git init -q -b main
Invoke-Git add .

Write-Host "Activating .githooks..."
Invoke-Git config core.hooksPath .githooks

Write-Host "Creating initial commit..."
Invoke-Git commit -q --no-verify -m "Initial scaffold from expo app template"

Write-Host ""
Write-Host "Done. Next steps:"
Write-Host "  1. npm run verify"
Write-Host "  2. gh repo create"
Write-Host "  3. Read SUBMISSION.md, then ask Claude to run the submission-doctor skill"
