param(
    [string]$ProjectDir = (Get-Location).Path,
    [string]$RepoPath,
    [Parameter(Mandatory = $true)][string]$OutFile
)

$ErrorActionPreference = 'Stop'

function Convert-TemplateText {
    param([string]$Text, $Renames)
    foreach ($r in @($Renames)) { $Text = $Text.Replace([string]$r.from, [string]$r.to) }
    return $Text
}

function Test-OwnedPath {
    param([string]$RelPath, [string[]]$Globs)
    foreach ($g in $Globs) {
        $pattern = [regex]::Escape($g)
        $pattern = $pattern.Replace('\*\*', '.*').Replace('\*', '[^/]*')
        if ($RelPath -match ('^' + $pattern + '$')) { return $true }
    }
    return $false
}

function Get-Normalized {
    param([string]$Text)
    return ($Text -replace "`r`n", "`n").TrimEnd("`n")
}

$stampPath = Join-Path $ProjectDir '.harness.json'
if (-not (Test-Path $stampPath)) { throw "No .harness.json in $ProjectDir. Run the harness-update skill's backfill flow first." }
$stamp = Get-Content $stampPath -Raw | ConvertFrom-Json

if (-not $RepoPath) {
    $RepoPath = & (Join-Path $PSScriptRoot 'resolve-repo.ps1') -RepoUrl $stamp.repoUrl | Select-Object -Last 1
}

$headCommit = ''
try { $headCommit = (git -C $RepoPath rev-parse origin/main 2>$null | Out-String).Trim() } catch { $headCommit = '' }
if ($LASTEXITCODE -ne 0 -or -not $headCommit) {
    $headCommit = (git -C $RepoPath rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Could not resolve a head commit in $RepoPath" }
}

git -C $RepoPath cat-file -e "$($stamp.lastUpdateCommit)^{commit}"
if ($LASTEXITCODE -ne 0) { throw "Stamped commit $($stamp.lastUpdateCommit) not found in $RepoPath (shallow clone? run 'git fetch --unshallow')" }

$templateDir = $stamp.templateDir
$manifestRaw = (git -C $RepoPath show "${headCommit}:$templateDir/harness-manifest.json") -join "`n"
if ($LASTEXITCODE -ne 0) { throw "harness-manifest.json missing from $templateDir at $headCommit" }
$ownedGlobs = [string[]]((($manifestRaw | ConvertFrom-Json).ownedPaths))

$diffLines = @(git -C $RepoPath diff --name-status "$($stamp.lastUpdateCommit)..$headCommit" -- $templateDir)
if ($LASTEXITCODE -ne 0) { throw 'git diff failed' }

$files = @()
foreach ($line in $diffLines) {
    if (-not $line) { continue }
    $parts = $line -split "`t"
    $status = $parts[0]
    $templatePath = if ($status -like 'R*') { $parts[2] } else { $parts[1] }
    $relPath = $templatePath.Substring($templateDir.Length + 1)
    if (-not (Test-OwnedPath -RelPath $relPath -Globs $ownedGlobs)) { continue }

    $projectRel = Convert-TemplateText -Text $relPath -Renames $stamp.renames
    $projectFile = Join-Path $ProjectDir $projectRel

    if ($status -eq 'D') {
        $class = if (Test-Path $projectFile) { 'deleted' } else { 'already-absent' }
    }
    elseif (-not (Test-Path $projectFile)) {
        $class = 'new'
    }
    else {
        $baseRaw = $null
        try { $baseRaw = (git -C $RepoPath show "$($stamp.lastUpdateCommit):$templatePath" 2>$null) -join "`n" } catch { $baseRaw = $null }
        if ($LASTEXITCODE -ne 0) { $baseRaw = $null }
        $current = Get-Content $projectFile -Raw
        if ($null -ne $baseRaw -and (Get-Normalized (Convert-TemplateText -Text $baseRaw -Renames $stamp.renames)) -eq (Get-Normalized $current)) {
            $class = 'clean'
        }
        else {
            $class = 'modified'
        }
    }
    $files += [pscustomobject]@{
        templatePath   = $templatePath
        projectPath    = $projectRel
        gitStatus      = $status
        classification = $class
    }
}

$result = [pscustomobject]@{
    projectDir       = $ProjectDir
    repoPath         = $RepoPath
    templateDir      = $templateDir
    lastUpdateCommit = $stamp.lastUpdateCommit
    headCommit       = $headCommit
    files            = @($files)
}
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[IO.File]::WriteAllText($OutFile, ($result | ConvertTo-Json -Depth 5), $utf8NoBom)
Write-Output "Wrote update manifest to $OutFile ($(@($files).Count) candidate file(s), head $headCommit)"
