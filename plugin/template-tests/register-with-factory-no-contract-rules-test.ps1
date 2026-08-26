. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    Invoke-Registration $fixture
    Assert (-not (Test-Path (Join-Path $fixture.Project '.agent-factory/contract-rules.md'))) 'no contract rules document is written'
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'register-with-factory: writes no contract rules document: passed'
