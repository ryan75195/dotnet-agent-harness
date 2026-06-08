$ErrorActionPreference = 'Stop'

$expoRoot = Split-Path -Parent $PSScriptRoot
$scaffoldDir = Join-Path $env:TEMP 'expo-template-smoke'

Write-Host "Cleaning previous smoke-test directory..."
if (Test-Path $scaffoldDir) {
    Remove-Item -Recurse -Force $scaffoldDir
}

& (Join-Path $expoRoot 'new-app.ps1') -Name SmokeTest -Destination $scaffoldDir

Push-Location $scaffoldDir
try {
    Write-Host "Installing dependencies..."
    npm install
    if ($LASTEXITCODE -ne 0) { throw 'npm install failed' }

    Write-Host "Typecheck..."
    npm run typecheck
    if ($LASTEXITCODE -ne 0) { throw 'typecheck failed' }

    Write-Host "Lint..."
    npm run lint
    if ($LASTEXITCODE -ne 0) { throw 'lint failed' }

    Write-Host "Dependency rules..."
    npm run depcruise
    if ($LASTEXITCODE -ne 0) { throw 'depcruise failed' }

    Write-Host "Test-file check..."
    npm run check-test-files
    if ($LASTEXITCODE -ne 0) { throw 'check-test-files failed' }

    Write-Host "Tests + coverage..."
    npx jest --coverage --silent
    if ($LASTEXITCODE -ne 0) { throw 'tests failed' }

    Write-Host "Seeded violation: no-comments must fire..."
    Set-Content -Path 'src\lib\seeded.ts' -Value "// seeded violation`nexport const seeded = 1;"
    npx eslint src/lib/seeded.ts
    if ($LASTEXITCODE -eq 0) { throw 'no-comments rule did not fire on a seeded violation' }
    Remove-Item 'src\lib\seeded.ts'

    Write-Host "Doctor must fail on a fresh scaffold (no icon, example bundle id)..."
    node scripts/submission-doctor.js
    if ($LASTEXITCODE -eq 0) { throw 'submission-doctor unexpectedly passed on a fresh scaffold' }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Expo template validation passed. Cleaning up..."
Remove-Item -Recurse -Force $scaffoldDir
Write-Host "Done."
