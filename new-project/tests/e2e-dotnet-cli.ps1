$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$dest = Join-Path $env:TEMP 'new-project-e2e-cli'
if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }

& (Join-Path $repo 'new-project.ps1') dotnet-cli NpE2ETool -Destination $dest
if ($LASTEXITCODE -ne 0) { throw "new-project exited $LASTEXITCODE" }
if (-not (Test-Path $dest)) { throw 'project dir was not created' }
$stampPath = Join-Path $dest '.harness.json'
if (-not (Test-Path $stampPath)) { throw '.harness.json missing' }
$stamp = Get-Content $stampPath -Raw | ConvertFrom-Json
if ($stamp.template -ne 'cli') { throw "stamp template is not cli: $($stamp.template)" }

Remove-Item -Recurse -Force $dest
Write-Host 'e2e-dotnet-cli: passed'
