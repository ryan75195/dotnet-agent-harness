param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('cli', 'etl-api', 'mcp')]
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
