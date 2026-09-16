param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('cli', 'durable', 'etl-api', 'mcp')]
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

    Write-Host "Verifying formatting (the scaffolded pre-commit gate runs this)..."
    dotnet format --verify-no-changes
    if ($LASTEXITCODE -ne 0) { throw "dotnet format --verify-no-changes found issues on a fresh scaffold" }

    if ($Template -eq 'durable') {
        Write-Host "Running scaffolded tests (Unit/Architecture/Analyzers)..."
        # Scoped like the pre-commit hook and the new-project dotnet-durable handler's
        # Verify step: Integration needs Azurite + Core Tools reachable, and this smoke
        # test must pass with neither installed. Push and watch the durable CI job (or
        # follow dotnet/README.md#development) to exercise Integration.
        $testProjects = @(
            'tests/SmokeTest.Tests.Unit',
            'tests/SmokeTest.Tests.Architecture',
            'tests/SmokeTest.Tests.Analyzers'
        )
        foreach ($project in $testProjects) {
            dotnet test $project --no-build --verbosity minimal
            if ($LASTEXITCODE -ne 0) { throw "Tests failed for $project" }
        }
    }
    else {
        Write-Host "Running scaffolded tests..."
        dotnet test --no-build --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
    }

    Write-Host "Factory readiness: setup.ps1 must write the policy and ship the label script..."
    git init -q -b main
    git config user.email smoke@test.local
    git config user.name smoke
    ./setup.ps1
    if ($LASTEXITCODE -ne 0) { throw "setup.ps1 failed" }

    $policyPath = Join-Path $scaffoldDir '.github/agent-factory.yml'
    if (-not (Test-Path $policyPath)) { throw 'agent-factory.yml missing after setup.ps1' }
    $policyContent = Get-Content $policyPath -Raw
    if ($policyContent -notmatch 'authorizedMaintainers: \[[^\]]+\]') { throw "agent-factory.yml missing authorizedMaintainers:`n$policyContent" }

    $labelsScript = Join-Path $scaffoldDir 'scripts/factory-labels.ps1'
    if (-not (Test-Path $labelsScript)) { throw 'scripts/factory-labels.ps1 missing from scaffold' }
    $labelOutput = (& $labelsScript 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "factory-labels.ps1 did not exit 0 without an origin remote (exit $LASTEXITCODE)`n$labelOutput" }
    if ($labelOutput -notmatch '(?i)origin') { throw "factory-labels.ps1 did not report a no-origin skip`n$labelOutput" }

    Write-Host "Feedback capture: harness_log_failure must append an event..."
    $gitBash = Join-Path (Split-Path (Split-Path (Get-Command git).Source)) 'bin\bash.exe'
    if (-not (Test-Path $gitBash)) { $gitBash = 'bash' }
    $feedbackDir = Join-Path $env:TEMP 'agent-harness-feedback-test-dotnet'
    if (Test-Path $feedbackDir) { Remove-Item -Recurse -Force $feedbackDir }
    $env:AGENT_HARNESS_FEEDBACK_DIR = ($feedbackDir -replace '\\', '/')
    git init -q -b main
    git config user.email smoke@test.local
    git config user.name smoke
    Set-Content '.harness.json' ('{ "template": "' + $Template + '", "stack": "dotnet" }')
    $savedEAP = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $markerOut = & $gitBash -c '. .githooks/harness-feedback.sh; harness_log_failure build ''error CS0000: seeded failure''' | Out-String
    $ErrorActionPreference = $savedEAP
    if ($markerOut -notmatch 'HARNESS-FEEDBACK: event ([0-9a-f]+)') { throw "marker not emitted: $markerOut" }
    $eventId = $Matches[1]
    $events = Get-Content (Join-Path $feedbackDir 'events.jsonl') -Raw
    if ($events -notmatch '"gate":"build"') { throw "build event not logged: $events" }
    if ($events -notmatch 'seeded failure') { throw 'outputTail missing from event' }

    Write-Host "Feedback capture: post-commit must link the fix..."
    git add .harness.json
    $savedEAP = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    git commit -q --no-verify -m "probe"
    & $gitBash .githooks/post-commit
    $ErrorActionPreference = $savedEAP
    $events = Get-Content (Join-Path $feedbackDir 'events.jsonl') -Raw
    if ($events -notmatch ('"id":"' + $eventId + '","fixCommit":"[0-9a-f]{40}"')) { throw 'fixCommit patch line missing' }
    Remove-Item Env:AGENT_HARNESS_FEEDBACK_DIR
    Remove-Item -Recurse -Force $feedbackDir
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Smoke test passed. Cleaning up..."
dotnet new uninstall $repoRoot
Remove-Item -Recurse -Force $scaffoldDir
Write-Host "Done."
