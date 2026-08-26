. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    Invoke-Registration $fixture
    $path = Join-Path $fixture.Project '.github/agent-factory.yml'
    Assert (Test-Path $path -PathType Leaf) 'the factory policy file is written'
    $content = [IO.File]::ReadAllText($path)
    $source = [IO.File]::ReadAllText((Join-Path $fixture.Factory '.github/agent-factory.yml'))
    Assert ($content -eq $source) 'policy is valid factory YAML with the factory policy values'
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'register-with-factory: writes the factory policy file: passed'
