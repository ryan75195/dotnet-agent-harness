param(
    [Parameter(Mandatory = $true)][string]$ProjectDir
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

& (Join-Path $PSScriptRoot 'write-factory-policy.ps1') -ProjectDir $ProjectDir

$setupSource = Join-Path $repoRoot 'templates/agent-factory-setup.sh'
$setupDestination = Join-Path $ProjectDir '.agent-factory/setup.sh'

if (-not (Test-Path -LiteralPath $setupDestination -PathType Leaf)) {
    $setupDir = Split-Path -Parent $setupDestination
    New-Item -ItemType Directory -Path $setupDir -Force | Out-Null

    $content = [IO.File]::ReadAllText($setupSource) -replace "`r`n", "`n" -replace "`r", "`n"
    $utf8NoBom = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText($setupDestination, $content, $utf8NoBom)

    try {
        $mode = [IO.File]::GetUnixFileMode($setupDestination)
        [IO.File]::SetUnixFileMode($setupDestination, $mode -bor [IO.UnixFileMode]::UserExecute)
    }
    catch [System.PlatformNotSupportedException] {
        # Unix file modes cannot be represented on Windows. The executable bit only
        # matters to the factory's Linux sandbox, so this is a no-op here by design.
    }
}

Write-Output 'Factory registration completed.'
