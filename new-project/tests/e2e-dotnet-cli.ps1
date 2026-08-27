$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$dest = Join-Path ([IO.Path]::GetTempPath()) 'new-project-e2e-cli'
if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
$oldGitConfig = $env:GIT_CONFIG_GLOBAL
$gitConfig = Join-Path ([IO.Path]::GetTempPath()) 'new-project-e2e-gitconfig'
$env:GIT_CONFIG_GLOBAL = $gitConfig
git config --global user.email 'contract@example.test'
git config --global user.name 'Contract Test'

try {
    $output = (& (Join-Path $repo 'new-project.ps1') dotnet-cli NpE2ETool -Destination $dest 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "new-project exited $LASTEXITCODE" }
    if (-not (Test-Path $dest)) { throw 'project dir was not created' }
    $stampPath = Join-Path $dest '.harness.json'
    if (-not (Test-Path $stampPath)) { throw '.harness.json missing' }
    $stamp = Get-Content $stampPath -Raw | ConvertFrom-Json
    if ($stamp.template -ne 'cli') { throw "stamp template is not cli: $($stamp.template)" }
    if ($output -notmatch 'Registering with agent-factory\.\.\.') { throw 'Failed Scaffold_reports_that_registration_was_attempted' }
} finally {
    if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
    if ($null -eq $oldGitConfig) { Remove-Item Env:GIT_CONFIG_GLOBAL -ErrorAction SilentlyContinue } else { $env:GIT_CONFIG_GLOBAL = $oldGitConfig }
    if (Test-Path $gitConfig) { Remove-Item -Force $gitConfig }
}
Write-Host 'e2e-dotnet-cli: passed'
