$ErrorActionPreference = 'Stop'
$testName = 'Scaffold_succeeds_and_reports_registration_skipped_when_source_is_unreachable'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$root = Join-Path ([IO.Path]::GetTempPath()) ('new-project-unreachable-' + [guid]::NewGuid().ToString('N'))
$dest = Join-Path $root 'project'
$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Path $bin -Force | Out-Null
$ghPath = Join-Path $bin 'gh'
[IO.File]::WriteAllText($ghPath, "#!/bin/sh`nexit 1`n")
[IO.File]::SetUnixFileMode($ghPath, [IO.UnixFileMode]::UserExecute -bor [IO.UnixFileMode]::UserRead)
$oldPath = $env:PATH
$oldGitConfig = $env:GIT_CONFIG_GLOBAL
$env:GIT_CONFIG_GLOBAL = Join-Path $root 'gitconfig'
git config --global user.email 'contract@example.test'
git config --global user.name 'Contract Test'

try {
    $env:PATH = "$bin$([IO.Path]::PathSeparator)$oldPath"
    $output = (& (Join-Path $repo 'new-project.ps1') dotnet-cli UnreachableFactoryTool -Destination $dest 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "Failed ${testName}: dispatcher exited $LASTEXITCODE`n$output" }
    if (-not (Test-Path $dest)) { throw 'project directory was not created' }
    if ($output -notmatch '(?i)Factory registration skipped') { throw "registration skip was not reported`n$output" }
} catch {
    if ($_.Exception.Message -like "Failed ${testName}:*") { throw }
    throw "Failed ${testName}: $($_.Exception.Message)"
} finally {
    $env:PATH = $oldPath
    if ($null -eq $oldGitConfig) { Remove-Item Env:GIT_CONFIG_GLOBAL -ErrorAction SilentlyContinue } else { $env:GIT_CONFIG_GLOBAL = $oldGitConfig }
    if (Test-Path $root) { Remove-Item -Recurse -Force $root }
}

Write-Host "e2e-registration-unreachable: ${testName}: passed"
