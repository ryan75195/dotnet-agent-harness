param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('cli', 'etl-api')]
    [string]$Template
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$scaffoldDir = Join-Path $env:TEMP "$Template-template-smoke"

Write-Host "Cleaning previous smoke-test directory..."
if (Test-Path $scaffoldDir) {
    Remove-Item -Recurse -Force $scaffoldDir
}

Write-Host "Installing templates from $repoRoot..."
dotnet new install $repoRoot --force

Write-Host "Scaffolding $Template project at $scaffoldDir..."
dotnet new $Template -n SmokeTest -o $scaffoldDir

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
dotnet new uninstall $repoRoot
Remove-Item -Recurse -Force $scaffoldDir
Write-Host "Done."
