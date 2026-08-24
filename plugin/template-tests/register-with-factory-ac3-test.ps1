. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    Invoke-Registration $fixture
    $path = Join-Path $fixture.Project '.agent-factory/lint.sh'
    Assert (Test-Path $path -PathType Leaf) 'AC-3 lint script is created'
    Assert (([IO.File]::GetUnixFileMode($path) -band [IO.UnixFileMode]::UserExecute) -ne 0) 'AC-3 lint script is executable'
    Assert (([IO.File]::ReadAllText($path)).Contains('dotnet format --verify-no-changes')) 'AC-3 lint script validates formatting'
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'AC-3: passed'
