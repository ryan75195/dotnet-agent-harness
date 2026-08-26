. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    $missing = Join-Path $fixture.Root 'missing-factory'
    $output = (Invoke-Registration $fixture $missing | Out-String)
    Assert ($output -match '(?i)skip') 'an unreachable factory source reports a skip'
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'register-with-factory: an unreachable source reports a skip: passed'
