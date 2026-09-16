. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    Install-FakeGh $fixture -Login 'octocat'
    $policyDir = Join-Path $fixture.Project '.github'
    New-Item -ItemType Directory -Path $policyDir -Force | Out-Null
    $customPolicy = "version: 1`nenabled: true`ndefaultProvider: opencode`nbaseBranch: main`nauthorizedMaintainers: [someone-else]`nconcurrencyLimit: 10`nretry:`n  maxAttempts: 6`n  backoffSeconds: 30`n"
    [IO.File]::WriteAllText((Join-Path $policyDir 'agent-factory.yml'), $customPolicy, [Text.UTF8Encoding]::new($false))

    Invoke-Registration $fixture

    $content = [IO.File]::ReadAllText((Join-Path $policyDir 'agent-factory.yml'))
    Assert ($content -eq $customPolicy) 'an existing policy file is never overwritten'
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'register-with-factory: never overwrites an existing policy file: passed'
