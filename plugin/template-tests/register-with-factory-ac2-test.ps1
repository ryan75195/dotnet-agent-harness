. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    Invoke-Registration $fixture
    $path = Join-Path $fixture.Project '.agent-factory/setup.sh'
    Assert (Test-Path $path -PathType Leaf) 'AC-2 setup script is created'
    Assert (([IO.File]::GetUnixFileMode($path) -band [IO.UnixFileMode]::UserExecute) -ne 0) 'AC-2 setup script is executable'
    Assert (([IO.File]::ReadAllText($path)).Contains('git config core.hooksPath .githooks')) 'AC-2 setup script configures hooks path'
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'AC-2: passed'
