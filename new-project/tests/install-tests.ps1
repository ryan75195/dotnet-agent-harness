$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$bin = Join-Path $env:TEMP 'np-install-test-bin'
if (Test-Path $bin) { Remove-Item -Recurse -Force $bin }

& (Join-Path $repo 'install.ps1') -BinDir $bin -NoPath -NoConfig | Out-Null

foreach ($f in 'new-project.cmd', 'new-project') {
    if (-not (Test-Path (Join-Path $bin $f))) { throw "shim '$f' was not created" }
}
$cmd = Get-Content (Join-Path $bin 'new-project.cmd') -Raw
if ($cmd -notmatch 'new-project\.ps1') { throw 'cmd shim does not reference new-project.ps1' }
if ($cmd -notmatch [regex]::Escape(($repo -replace '/', '\'))) { throw 'cmd shim does not reference the repo path' }
$sh = Get-Content (Join-Path $bin 'new-project') -Raw
if ($sh -notmatch 'new-project\.ps1') { throw 'sh shim does not reference new-project.ps1' }

Remove-Item -Recurse -Force $bin

$emptyCfg = Join-Path $env:TEMP 'np-install-empty-config.json'
Set-Content -Path $emptyCfg -Value '' -NoNewline
$bin2 = Join-Path $env:TEMP 'np-install-test-bin2'
if (Test-Path $bin2) { Remove-Item -Recurse -Force $bin2 }
& (Join-Path $repo 'install.ps1') -BinDir $bin2 -NoPath -ConfigPath $emptyCfg | Out-Null
$cfg = Get-Content $emptyCfg -Raw | ConvertFrom-Json
if ($cfg.repoPath -ne $repo) { throw "empty config was not populated with repoPath (got '$($cfg.repoPath)')" }

$existingCfg = Join-Path $env:TEMP 'np-install-existing-config.json'
Set-Content -Path $existingCfg -Value '{ "repoPath": "C:/somewhere/else" }'
& (Join-Path $repo 'install.ps1') -BinDir $bin2 -NoPath -ConfigPath $existingCfg | Out-Null
$cfg2 = Get-Content $existingCfg -Raw | ConvertFrom-Json
if ($cfg2.repoPath -ne 'C:/somewhere/else') { throw "existing repoPath was clobbered (got '$($cfg2.repoPath)')" }

Remove-Item -Force $emptyCfg, $existingCfg
Remove-Item -Recurse -Force $bin2

Write-Host 'install-tests: passed'
