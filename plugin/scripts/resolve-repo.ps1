param(
    [string]$RepoUrl = 'https://github.com/ryan75195/dotnet-agent-harness',
    [string]$ConfigPath = (Join-Path $HOME '.agent-harness/config.json'),
    [switch]$NoClone
)

$ErrorActionPreference = 'Stop'

if (Test-Path $ConfigPath) {
    $cfg = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    if ($cfg.repoPath) {
        if (Test-Path (Join-Path $cfg.repoPath '.git')) {
            git -C $cfg.repoPath fetch --quiet origin main
            if ($LASTEXITCODE -ne 0) {
                Write-Warning 'fetch failed (offline or no origin) - using checkout as-is'
                $global:LASTEXITCODE = 0
            }
            Write-Output $cfg.repoPath
            exit 0
        }
        throw "config.json repoPath '$($cfg.repoPath)' is not a git checkout"
    }
}

if ($NoClone) { throw "No usable local checkout configured at $ConfigPath and cloning is disabled" }

$clonePath = Join-Path $env:TEMP 'agent-harness-repo'
if (Test-Path (Join-Path $clonePath '.git')) {
    git -C $clonePath fetch --quiet origin main
    if ($LASTEXITCODE -eq 0) {
        git -C $clonePath merge --ff-only --quiet origin/main | Out-Null
    }
    else {
        Write-Warning 'fetch failed (offline?) - using cached clone as-is'
        $global:LASTEXITCODE = 0
    }
}
else {
    if (Test-Path $clonePath) { Remove-Item -Recurse -Force $clonePath }
    git clone --quiet $RepoUrl $clonePath
    if ($LASTEXITCODE -ne 0) { throw "Failed to clone $RepoUrl. Configure a local checkout in $ConfigPath as { `"repoPath`": `"...`" }" }
}
Write-Output $clonePath
