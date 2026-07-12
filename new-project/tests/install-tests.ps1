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
Write-Host 'install-tests: passed'
