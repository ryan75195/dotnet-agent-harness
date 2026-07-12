[CmdletBinding()]
param(
    [string]$BinDir = (Join-Path $HOME '.agent-harness/bin'),
    [switch]$NoPath,
    [switch]$NoConfig,
    [string]$ConfigPath = (Join-Path $HOME '.agent-harness/config.json')
)
$ErrorActionPreference = 'Stop'

$repo = $PSScriptRoot
if (-not (Test-Path (Join-Path $repo 'new-project.ps1'))) { throw "new-project.ps1 not found at $repo" }
$winPath = $repo -replace '/', '\'

New-Item -ItemType Directory -Force -Path $BinDir | Out-Null

$cmdShim = "@echo off`r`npowershell -NoProfile -ExecutionPolicy Bypass -File `"$winPath\new-project.ps1`" %*`r`n"
$shShim = "#!/bin/sh`nexec powershell -NoProfile -ExecutionPolicy Bypass -File `"$winPath\new-project.ps1`" `"`$@`"`n"

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[IO.File]::WriteAllText((Join-Path $BinDir 'new-project.cmd'), $cmdShim, $utf8NoBom)
[IO.File]::WriteAllText((Join-Path $BinDir 'new-project'), $shShim, $utf8NoBom)

if (-not $NoPath) {
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $entries = @()
    if ($userPath) { $entries = $userPath -split ';' }
    if ($entries -notcontains $BinDir) {
        $newPath = ((@($entries | Where-Object { $_ }) + $BinDir) -join ';')
        [Environment]::SetEnvironmentVariable('Path', $newPath, 'User')
        Write-Host "Added $BinDir to your User PATH. Open a new terminal for it to take effect."
    }
    else {
        Write-Host "$BinDir is already on your User PATH."
    }
}

if (-not $NoConfig) {
    $cfgDir = Split-Path -Parent $ConfigPath
    New-Item -ItemType Directory -Force -Path $cfgDir | Out-Null
    $cfg = [pscustomobject]@{}
    if (Test-Path $ConfigPath) {
        $raw = Get-Content $ConfigPath -Raw
        if ($raw -and $raw.Trim()) {
            $parsed = $raw | ConvertFrom-Json
            if ($parsed) { $cfg = $parsed }
        }
    }
    if (-not $cfg.repoPath) {
        $cfg | Add-Member -NotePropertyName repoPath -NotePropertyValue $repo -Force
        ($cfg | ConvertTo-Json -Depth 5) | Set-Content -Path $ConfigPath -Encoding UTF8
        Write-Host "Set repoPath in $ConfigPath so agent skills resolve to this checkout."
    }
}

Write-Host ''
Write-Host "Installed 'new-project'. Try:  new-project   (shows usage)"
Write-Host "Uninstall: remove '$BinDir' from your User PATH and delete the two shims."
