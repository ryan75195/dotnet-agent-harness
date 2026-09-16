. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    Install-FakeGh $fixture -Login 'octocat'
    Invoke-Registration $fixture
    $path = Join-Path $fixture.Project '.agent-factory/setup.sh'
    Assert (Test-Path $path -PathType Leaf) 'the setup script is written'
    Assert (([IO.File]::GetUnixFileMode($path) -band [IO.UnixFileMode]::UserExecute) -ne 0) 'the setup script is executable'
    Assert (([IO.File]::ReadAllText($path)).Contains('git config core.hooksPath .githooks')) 'setup script configures hooks path'
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'register-with-factory: writes an executable setup script from the harness-local template: passed'
