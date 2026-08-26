param(
    [Parameter(Mandatory = $true)][string]$ProjectDir,
    [string]$FactoryRepoPath
)

$ErrorActionPreference = 'Stop'

$factoryFiles = [ordered]@{
    '.github/agent-factory.yml' = '.github/agent-factory.yml'
    '.agent-factory/setup.sh' = '.agent-factory/setup.sh'
    '.agent-factory/lint.sh' = '.agent-factory/lint.sh'
}

function Get-FactoryFileContent([string]$RelativePath) {
    if ($FactoryRepoPath) {
        $sourcePath = Join-Path $FactoryRepoPath $RelativePath
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw "Factory file '$RelativePath' was not found in '$FactoryRepoPath'."
        }
        return [IO.File]::ReadAllText($sourcePath)
    }

    $encoded = & gh api "repos/ryan75195/agent-factory/contents/$RelativePath" --jq '.content'
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($encoded -join ''))) {
        throw "Could not fetch '$RelativePath' from agent-factory."
    }
    return [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(($encoded -join '').Trim()))
}

try {
    $contents = [ordered]@{}
    foreach ($file in $factoryFiles.Keys) {
        $contents[$file] = Get-FactoryFileContent $file
    }
}
catch {
    Write-Output "Factory registration skipped: $($_.Exception.Message)"
    exit 0
}

$utf8NoBom = [Text.UTF8Encoding]::new($false)
$shellFiles = @('.agent-factory/setup.sh', '.agent-factory/lint.sh')
foreach ($relativePath in $factoryFiles.Keys) {
    $destination = Join-Path $ProjectDir $relativePath
    if (Test-Path -LiteralPath $destination -PathType Leaf) {
        continue
    }

    $parent = Split-Path -Parent $destination
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $content = $contents[$relativePath]
    if ($relativePath -in $shellFiles) {
        $content = $content -replace "`r`n", "`n" -replace "`r", "`n"
    }
    [IO.File]::WriteAllText($destination, $content, $utf8NoBom)

    if ($relativePath -in $shellFiles) {
        $mode = [IO.File]::GetUnixFileMode($destination)
        [IO.File]::SetUnixFileMode($destination, $mode -bor [IO.UnixFileMode]::UserExecute)
    }
}

Write-Output 'Factory registration completed.'
