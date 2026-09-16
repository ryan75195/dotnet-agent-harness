. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    Install-FakeGh $fixture -Fail
    $output = (Invoke-Registration $fixture | Out-String)
    $path = Join-Path $fixture.Project '.github/agent-factory.yml'
    Assert (Test-Path $path -PathType Leaf) 'the factory policy file is still written when gh is unavailable'
    $content = [IO.File]::ReadAllText($path)
    Assert ($content.Contains('authorizedMaintainers: [REPLACE_WITH_YOUR_GITHUB_LOGIN]')) 'policy falls back to the placeholder login'
    Assert ($output -match '(?i)REPLACE_WITH_YOUR_GITHUB_LOGIN') 'a clear message names the placeholder to fix'
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'register-with-factory: falls back to a placeholder login when gh is unavailable: passed'
