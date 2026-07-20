param(
    [ValidateSet('app', 'tv-app')]
    [string]$Template = 'app'
)

$ErrorActionPreference = 'Stop'
# In pwsh 7.3+, $PSNativeCommandUseErrorActionPreference defaults to $true which causes
# any native command with a non-zero exit code to throw a terminating error, bypassing
# our manual $LASTEXITCODE checks. We disable it here so the script can intentionally
# run commands that exit non-zero (e.g. eslint on a seeded violation, submission-doctor).
if ($null -ne (Get-Variable -Name PSNativeCommandUseErrorActionPreference -Scope Global -ErrorAction SilentlyContinue)) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$expoRoot = Split-Path -Parent $PSScriptRoot
$suffix = if ($Template -eq 'tv-app') { '-tv' } else { '' }
$scaffoldRoot = Join-Path $env:SystemDrive 'agent-harness-template-tests'
$createdScaffoldRoot = -not (Test-Path -LiteralPath $scaffoldRoot)
[System.IO.Directory]::CreateDirectory($scaffoldRoot) | Out-Null
$scaffoldDir = Join-Path $scaffoldRoot "expo-template-smoke$suffix-$PID"

function Remove-ScaffoldDirectory([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        return
    }
    $emptyDir = Join-Path $scaffoldRoot "expo-template-empty-$PID"
    [System.IO.Directory]::CreateDirectory($emptyDir) | Out-Null
    robocopy $emptyDir $path /MIR /NFL /NDL /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy cleanup failed with exit code $LASTEXITCODE"
    }
    $global:LASTEXITCODE = 0
    [System.IO.Directory]::Delete("\\?\$path", $true)
    [System.IO.Directory]::Delete($emptyDir, $true)
}

Write-Host "Preparing smoke-test directory..."
Remove-ScaffoldDirectory $scaffoldDir

& (Join-Path $expoRoot 'new-app.ps1') -Name SmokeTest -Destination $scaffoldDir -Template $Template

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

    Write-Host "config-doctor must nudge on a fresh scaffold and exit 0..."
    $configDoctorOut = node scripts/config-doctor.js | Out-String
    if ($LASTEXITCODE -ne 0) { throw "config-doctor exited non-zero ($LASTEXITCODE) on a fresh scaffold" }
    if ($configDoctorOut -notmatch 'first-run-setup') { throw 'config-doctor did not emit a first-run-setup nudge on a fresh scaffold' }
    if ($configDoctorOut -notmatch 'ACTION:') { throw 'config-doctor nudge missing the ACTION line' }

    Write-Host "SessionStart hook + first-run-setup skill must ship in the scaffold..."
    if (-not (Test-Path (Join-Path $scaffoldDir '.claude\hooks\session-config-check.sh'))) { throw 'session-config-check.sh missing from scaffold' }
    if (-not (Test-Path (Join-Path $scaffoldDir '.claude\skills\first-run-setup\SKILL.md'))) { throw 'first-run-setup skill missing from scaffold' }
    $settingsText = Get-Content (Join-Path $scaffoldDir '.claude\settings.json') -Raw
    if ($settingsText -notmatch 'SessionStart') { throw 'settings.json missing SessionStart hook wiring' }

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

    if ($Template -eq 'tv-app') {
        Write-Host "Android TV production config must accept the Android RevenueCat key..."
        $env:NODE_ENV = 'production'
        $env:EXPO_TV_PLATFORM = 'android'
        $env:EXPO_PUBLIC_REVENUECAT_ANDROID_API_KEY = 'android-smoke-key'
        node -e "require('./app.config.js')"
        $androidConfigExit = $LASTEXITCODE
        Remove-Item Env:NODE_ENV
        Remove-Item Env:EXPO_TV_PLATFORM
        Remove-Item Env:EXPO_PUBLIC_REVENUECAT_ANDROID_API_KEY
        if ($androidConfigExit -ne 0) { throw "Android TV production config rejected the Android RevenueCat key (exit $androidConfigExit)" }
    }

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

    if ($Template -eq 'tv-app') {
        Write-Host "TV configuration and focus components must ship in the scaffold..."
        $packageText = Get-Content (Join-Path $scaffoldDir 'package.json') -Raw
        if ($packageText -notmatch 'react-native-tvos') { throw 'TV scaffold does not depend on react-native-tvos' }
        if ($packageText -notmatch '@react-native-tvos/config-tv') { throw 'TV scaffold does not depend on the TV config plugin' }
        if ($packageText -notmatch 'expo-video') { throw 'TV scaffold does not include expo-video' }
        $configText = Get-Content (Join-Path $scaffoldDir 'app.config.js') -Raw
        if ($configText -notmatch "orientation: 'landscape'") { throw 'TV scaffold does not force landscape orientation' }
        if ($configText -notmatch '@react-native-tvos/config-tv') { throw 'TV scaffold does not enable the TV config plugin' }
        if (-not (Test-Path (Join-Path $scaffoldDir 'src\components\FocusButton.tsx'))) { throw 'TV scaffold does not include FocusButton' }
        if (-not (Test-Path (Join-Path $scaffoldDir 'src\app\(app)\library.tsx'))) { throw 'TV scaffold does not include a library route' }
    }

    Write-Host "Doctor must flag missing account-deletion endpoint when auth is enabled..."
    $env:EXPO_PUBLIC_AUTH0_DOMAIN = 'smoke.auth0.com'
    $env:EXPO_PUBLIC_AUTH0_CLIENT_ID = 'smoke-client'
    $deleteDoctorOut = node scripts/submission-doctor.js 2>&1 | Out-String
    Remove-Item Env:EXPO_PUBLIC_AUTH0_DOMAIN
    Remove-Item Env:EXPO_PUBLIC_AUTH0_CLIENT_ID
    if ($deleteDoctorOut -notmatch 'EXPO_PUBLIC_ACCOUNT_DELETE_URL required when auth is enabled') {
        throw 'submission-doctor did not flag the missing account-deletion endpoint'
    }

    Write-Host "Feedback capture: blocked commit must log an event..."
    $feedbackDir = Join-Path $env:TEMP 'agent-harness-feedback-test'
    if (Test-Path $feedbackDir) { Remove-Item -Recurse -Force $feedbackDir }
    $env:AGENT_HARNESS_FEEDBACK_DIR = ($feedbackDir -replace '\\', '/')
    git init -q -b main
    git config user.email smoke@test.local
    git config user.name smoke
    git checkout -q -b capture-test
    git config core.hooksPath .githooks
    Set-Content -Path '.harness.json' -Value '{ "template": "expo-app", "stack": "expo" }'
    Set-Content -Path 'src\lib\seeded.ts' -Value "// seeded violation`nexport const seeded = 1;"
    git add .
    $savedEAP = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $commitOut = git commit -m "capture probe" 2>&1 | Out-String
    $ErrorActionPreference = $savedEAP
    if ($LASTEXITCODE -eq 0) { throw 'commit unexpectedly succeeded with a seeded lint violation' }
    if ($commitOut -notmatch 'HARNESS-FEEDBACK: event ([0-9a-f]+)') { throw "no HARNESS-FEEDBACK marker in commit output:`n$commitOut" }
    $eventId = $Matches[1]
    $events = Get-Content (Join-Path $feedbackDir 'events.jsonl') -Raw
    if ($events -notmatch ('"id":"' + $eventId + '"')) { throw 'event id not found in events.jsonl' }
    if ($events -notmatch '"gate":"lint"') { throw "event gate is not lint:`n$events" }
    if (-not (Test-Path (Join-Path $feedbackDir "diffs/$eventId.failure.patch"))) { throw 'failure patch missing' }

    Write-Host "Feedback capture: next passing commit must link the fix..."
    git rm -q --cached src/lib/seeded.ts
    Remove-Item 'src\lib\seeded.ts'
    git commit -q -m "capture probe fixed"
    if ($LASTEXITCODE -ne 0) { throw 'fix commit failed' }
    $events = Get-Content (Join-Path $feedbackDir 'events.jsonl') -Raw
    if ($events -notmatch ('"id":"' + $eventId + '","fixCommit":"[0-9a-f]{40}"')) { throw 'fixCommit patch line missing' }
    if (-not (Test-Path (Join-Path $feedbackDir "diffs/$eventId.fix.patch"))) { throw 'fix patch missing' }
    if (Test-Path '.git\harness-pending-event') { throw 'pending-event marker not cleaned up' }
    Remove-Item Env:AGENT_HARNESS_FEEDBACK_DIR
    Remove-Item -Recurse -Force $feedbackDir
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Expo template validation passed. Cleaning up..."
Remove-ScaffoldDirectory $scaffoldDir
if ($createdScaffoldRoot) {
    [System.IO.Directory]::Delete($scaffoldRoot, $true)
}
Write-Host "Done."
exit 0
