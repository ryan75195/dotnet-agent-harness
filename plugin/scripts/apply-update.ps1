param(
    [Parameter(Mandatory = $true)][string]$ManifestPath
)

$ErrorActionPreference = 'Stop'

function Convert-TemplateText {
    param([string]$Text, $Renames)
    foreach ($r in @($Renames)) { $Text = $Text.Replace([string]$r.from, [string]$r.to) }
    return $Text
}

$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$stamp = Get-Content (Join-Path $manifest.projectDir '.harness.json') -Raw | ConvertFrom-Json
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

$applied = @()
$flagged = @()
foreach ($f in @($manifest.files)) {
    if ($f.classification -eq 'clean' -or $f.classification -eq 'new') {
        $content = (git -C $manifest.repoPath show "$($manifest.headCommit):$($f.templatePath)") -join "`n"
        if ($LASTEXITCODE -ne 0) { throw "git show failed for $($f.templatePath)" }
        $content = Convert-TemplateText -Text $content -Renames $stamp.renames
        if (-not $content.EndsWith("`n")) { $content += "`n" }
        $target = Join-Path $manifest.projectDir $f.projectPath
        $targetDir = Split-Path -Parent $target
        if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Force $targetDir | Out-Null }
        [IO.File]::WriteAllText($target, $content, $utf8NoBom)
        $applied += $f.projectPath
    }
    elseif ($f.classification -eq 'modified' -or $f.classification -eq 'deleted') {
        $flagged += $f
    }
}

Write-Output "Applied $($applied.Count) file(s):"
$applied | ForEach-Object { Write-Output "  $_" }
Write-Output "Flagged for manual merge: $(@($flagged).Count)"
$flagged | ForEach-Object { Write-Output "  $($_.projectPath) [$($_.classification)]" }
