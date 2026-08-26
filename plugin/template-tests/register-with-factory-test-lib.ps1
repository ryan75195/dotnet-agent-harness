$ErrorActionPreference = 'Stop'

$script:repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$script:registrationScript = Join-Path $script:repo 'plugin/scripts/register-with-factory.ps1'

function Assert($condition, $message) {
    if (-not $condition) { throw "FAIL: $message" }
}

function New-RegistrationFixture {
    $root = Join-Path ([IO.Path]::GetTempPath()) ("factory-registration-" + [guid]::NewGuid().ToString('N'))
    $factory = Join-Path $root 'factory'
    $project = Join-Path $root 'project'
    New-Item -ItemType Directory -Path (Join-Path $factory '.github') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $factory '.agent-factory') -Force | Out-Null
    New-Item -ItemType Directory -Path $project -Force | Out-Null

    $policy = @"
version: 1
enabled: true
defaultProvider: opencode
baseBranch: main
authorizedMaintainers: [ryan75195]
concurrencyLimit: 10
retry:
  maxAttempts: 6
  backoffSeconds: 30
"@
    [IO.File]::WriteAllText((Join-Path $factory '.github/agent-factory.yml'), $policy, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $factory '.agent-factory/setup.sh'), "#!/bin/sh`ngit config core.hooksPath .githooks`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $factory '.agent-factory/lint.sh'), "#!/bin/sh`ndotnet format --verify-no-changes`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $factory '.agent-factory/contract-rules.md'), '# factory-only rules', [Text.UTF8Encoding]::new($false))

    [pscustomobject]@{ Root = $root; Factory = $factory; Project = $project }
}

function Invoke-Registration($fixture, [string]$factoryPath = $fixture.Factory) {
    & $script:registrationScript -ProjectDir $fixture.Project -FactoryRepoPath $factoryPath
}

function Remove-RegistrationFixture($fixture) {
    if ($fixture -and (Test-Path $fixture.Root)) { Remove-Item -Recurse -Force $fixture.Root }
}
