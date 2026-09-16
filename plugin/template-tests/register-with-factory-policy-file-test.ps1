. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    Install-FakeGh $fixture -Login 'octocat'
    Invoke-Registration $fixture
    $path = Join-Path $fixture.Project '.github/agent-factory.yml'
    Assert (Test-Path $path -PathType Leaf) 'the factory policy file is written'
    $content = [IO.File]::ReadAllText($path)
    $expected = "version: 1`nenabled: true`ndefaultProvider: opencode`nbaseBranch: main`nauthorizedMaintainers: [octocat]`nconcurrencyLimit: 2`nretry:`n  maxAttempts: 3`n  backoffSeconds: 60`n"
    Assert ($content -eq $expected) "policy content matches exactly, got:`n$content"
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'register-with-factory: writes the factory policy file with the scaffolder login: passed'
