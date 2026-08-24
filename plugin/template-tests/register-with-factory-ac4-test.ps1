. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    Invoke-Registration $fixture
    Assert (-not (Test-Path (Join-Path $fixture.Project '.agent-factory/contract-rules.md'))) 'AC-4 project has no contract rules document'
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'AC-4: passed'
