$ErrorActionPreference = 'Stop'
$testName = 'Scaffold_succeeds_and_falls_back_to_a_placeholder_login_when_gh_is_unavailable'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$root = Join-Path ([IO.Path]::GetTempPath()) ('new-project-placeholder-' + [guid]::NewGuid().ToString('N'))
$dest = Join-Path $root 'project'
$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Path $bin -Force | Out-Null
$ghPath = Join-Path $bin 'gh.cmd'
[IO.File]::WriteAllText($ghPath, "@echo off`r`nexit /b 1`r`n")
$oldPath = $env:PATH
$oldGitConfig = $env:GIT_CONFIG_GLOBAL
$env:GIT_CONFIG_GLOBAL = Join-Path $root 'gitconfig'
git config --global user.email 'contract@example.test'
git config --global user.name 'Contract Test'

try {
    $env:PATH = "$bin$([IO.Path]::PathSeparator)$oldPath"
    $output = (& (Join-Path $repo 'new-project.ps1') dotnet-cli PlaceholderLoginTool -Destination $dest 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "Failed ${testName}: dispatcher exited $LASTEXITCODE`n$output" }
    if (-not (Test-Path $dest)) { throw 'project directory was not created' }
    $policyPath = Join-Path $dest '.github/agent-factory.yml'
    if (-not (Test-Path $policyPath)) { throw "Failed ${testName}: policy file was not written" }
    $policy = [IO.File]::ReadAllText($policyPath)
    if ($policy -notmatch 'authorizedMaintainers: \[REPLACE_WITH_YOUR_GITHUB_LOGIN\]') {
        throw "Failed ${testName}: policy does not carry the placeholder login`n$policy"
    }
} catch {
    if ($_.Exception.Message -like "Failed ${testName}:*") { throw }
    throw "Failed ${testName}: $($_.Exception.Message)"
} finally {
    $env:PATH = $oldPath
    if ($null -eq $oldGitConfig) { Remove-Item Env:GIT_CONFIG_GLOBAL -ErrorAction SilentlyContinue } else { $env:GIT_CONFIG_GLOBAL = $oldGitConfig }
    if (Test-Path $root) { Remove-Item -Recurse -Force $root }
}

Write-Host "e2e-registration-placeholder: ${testName}: passed"
