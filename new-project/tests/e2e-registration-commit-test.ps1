$ErrorActionPreference = 'Stop'
$testName = 'Scaffold_registration_files_are_in_the_initial_commit'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$root = Join-Path ([IO.Path]::GetTempPath()) ('new-project-registration-' + [guid]::NewGuid().ToString('N'))
$dest = Join-Path $root 'project'
$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Path $bin -Force | Out-Null

# Return valid, distinct factory payloads for each gh API request.
$policy = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("version: 1`ndefaultProvider: opencode`n"))
$setup = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("#!/bin/sh`ngit config core.hooksPath .githooks`n"))
$lint = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("#!/bin/sh`ndotnet format --verify-no-changes`n"))
$gh = @"
#!/bin/sh
case "`$2" in
  *agent-factory.yml) printf '%s\n' '$policy' ;;
  *setup.sh) printf '%s\n' '$setup' ;;
  *lint.sh) printf '%s\n' '$lint' ;;
  *) exit 1 ;;
esac
"@
$ghPath = Join-Path $bin 'gh'
[IO.File]::WriteAllText($ghPath, $gh)
[IO.File]::SetUnixFileMode($ghPath, [IO.UnixFileMode]::UserExecute -bor [IO.UnixFileMode]::UserRead)
$oldPath = $env:PATH
$oldGitConfig = $env:GIT_CONFIG_GLOBAL
$env:GIT_CONFIG_GLOBAL = Join-Path $root 'gitconfig'
git config --global user.email 'contract@example.test'
git config --global user.name 'Contract Test'

try {
    $env:PATH = "$bin$([IO.Path]::PathSeparator)$oldPath"
    $output = (& (Join-Path $repo 'new-project.ps1') dotnet-cli InitialCommitTool -Destination $dest 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "Failed ${testName}: dispatcher exited $LASTEXITCODE`n$output" }

    $firstCommit = (git -C $dest log --reverse --format=%H | Select-Object -First 1)
    if (-not $firstCommit) { throw "Failed ${testName}: no initial commit was created" }
    $policyInCommit = git -C $dest show "$firstCommit`:.github/agent-factory.yml" 2>$null | Out-String
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($policyInCommit)) {
        throw "Failed ${testName}: policy file is absent from the initial commit"
    }
} catch {
    if ($_.Exception.Message -like "Failed ${testName}:*") { throw }
    throw "Failed ${testName}: $($_.Exception.Message)"
} finally {
    $env:PATH = $oldPath
    if ($null -eq $oldGitConfig) { Remove-Item Env:GIT_CONFIG_GLOBAL -ErrorAction SilentlyContinue } else { $env:GIT_CONFIG_GLOBAL = $oldGitConfig }
    if (Test-Path $root) { Remove-Item -Recurse -Force $root }
}

Write-Host "e2e-registration-commit: ${testName}: passed"
