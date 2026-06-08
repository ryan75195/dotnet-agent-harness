$ErrorActionPreference = 'Stop'
# In pwsh 7.3+, $PSNativeCommandUseErrorActionPreference defaults to $true which causes
# any native command with a non-zero exit code to throw a terminating error, bypassing
# our manual $LASTEXITCODE checks. We disable it here so the script can intentionally
# run commands that exit non-zero (e.g. eslint on a seeded violation, submission-doctor).
if ($null -ne (Get-Variable -Name PSNativeCommandUseErrorActionPreference -Scope Global -ErrorAction SilentlyContinue)) {
    $PSNativeCommandUseErrorActionPreference = $false
}

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

    Write-Host "Partial auth config must fail the production config load..."
    $env:NODE_ENV = 'production'
    $env:EXPO_PUBLIC_REVENUECAT_IOS_API_KEY = 'smoke-key'
    $env:EXPO_PUBLIC_AUTH0_DOMAIN = 'smoke.auth0.com'
    node -e "try { require('./app.config.js'); process.exit(0) } catch (e) { process.exit(7) }"
    $partialExit = $LASTEXITCODE
    Remove-Item Env:NODE_ENV
    Remove-Item Env:EXPO_PUBLIC_REVENUECAT_IOS_API_KEY
    Remove-Item Env:EXPO_PUBLIC_AUTH0_DOMAIN
    if ($partialExit -ne 7) { throw "partial auth config did not fail app.config.js (exit $partialExit)" }

    Write-Host "CI workflow must ship in the scaffold..."
    $ciPath = Join-Path $scaffoldDir '.github\workflows\ci.yml'
    if (-not (Test-Path $ciPath)) { throw "ci.yml missing from scaffold at $ciPath" }
    $ciText = Get-Content $ciPath -Raw
    if ($ciText -notmatch 'name:\s*ci') { throw 'ci.yml missing the workflow name' }
    if ($ciText -notmatch 'npm ci') { throw 'ci.yml does not run npm ci' }
    if ($ciText -notmatch 'npm run verify') { throw 'ci.yml does not run npm run verify' }

    Write-Host "expo-router root layout must ship in the scaffold..."
    $layoutPath = Join-Path $scaffoldDir 'src\app\_layout.tsx'
    if (-not (Test-Path $layoutPath)) { throw "src/app/_layout.tsx missing from scaffold at $layoutPath" }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Expo template validation passed. Cleaning up..."
Remove-Item -Recurse -Force $scaffoldDir -ErrorAction SilentlyContinue
Write-Host "Done."
exit 0
