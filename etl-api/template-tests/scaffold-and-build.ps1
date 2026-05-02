$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$contentDir = Join-Path $repoRoot "content"
$scaffoldDir = Join-Path $env:TEMP "etl-api-template-smoke"

Write-Host "Cleaning previous smoke-test directory..."
if (Test-Path $scaffoldDir) {
    Remove-Item -Recurse -Force $scaffoldDir
}

Write-Host "Installing template from $contentDir..."
dotnet new install $contentDir --force

Write-Host "Scaffolding test project at $scaffoldDir..."
dotnet new etl-api -n SmokeTest -o $scaffoldDir

Write-Host "Building scaffolded project..."
Push-Location $scaffoldDir
try {
    dotnet build
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }

    Write-Host "Running scaffolded tests..."
    dotnet test --no-build --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Smoke test passed. Cleaning up..."
dotnet new uninstall $contentDir
Remove-Item -Recurse -Force $scaffoldDir
Write-Host "Done."
