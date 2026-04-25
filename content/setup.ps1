$ErrorActionPreference = 'Stop'

Write-Host "Initializing git repo..."
git init -q
git add .

Write-Host "Activating .githooks..."
git config core.hooksPath .githooks

Write-Host "Creating initial commit..."
git commit -q --no-verify -m "Initial scaffold from etl-api template"

Write-Host ""
Write-Host "Done. Next steps:"
Write-Host "  1. dotnet restore; dotnet build"
Write-Host "  2. gh repo create"
Write-Host "  3. gh issue create --title '...'"
