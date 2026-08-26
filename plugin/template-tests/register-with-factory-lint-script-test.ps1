. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    Invoke-Registration $fixture
    $path = Join-Path $fixture.Project '.agent-factory/lint.sh'
    Assert (Test-Path $path -PathType Leaf) 'the lint script is written'
    Assert (([IO.File]::GetUnixFileMode($path) -band [IO.UnixFileMode]::UserExecute) -ne 0) 'the lint script is executable'
    Assert (([IO.File]::ReadAllText($path)).Contains('dotnet format --verify-no-changes')) 'lint script validates formatting'
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'register-with-factory: writes an executable lint script: passed'
