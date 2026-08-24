. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    Invoke-Registration $fixture
    $path = Join-Path $fixture.Project '.github/agent-factory.yml'
    Assert (Test-Path $path -PathType Leaf) 'AC-1 policy file is created'
    $content = [IO.File]::ReadAllText($path)
    $source = [IO.File]::ReadAllText((Join-Path $fixture.Factory '.github/agent-factory.yml'))
    Assert ($content -eq $source) 'AC-1 policy is valid factory YAML with the factory policy values'
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'AC-1: passed'
