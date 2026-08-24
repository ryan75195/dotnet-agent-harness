. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    $missing = Join-Path $fixture.Root 'missing-factory'
    $output = (Invoke-Registration $fixture $missing | Out-String)
    Assert ($output -match '(?i)skip') 'AC-6 reports registration was skipped'
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'AC-6: passed'
