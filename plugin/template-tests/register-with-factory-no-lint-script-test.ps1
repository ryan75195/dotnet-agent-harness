. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    Install-FakeGh $fixture -Login 'octocat'
    Invoke-Registration $fixture
    Assert (-not (Test-Path (Join-Path $fixture.Project '.agent-factory/lint.sh'))) 'no lint script is written'
    Assert (-not (Test-Path (Join-Path $fixture.Project '.agent-factory/contract-rules.md'))) 'no contract rules document is written'
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'register-with-factory: writes no lint script or contract rules document: passed'
