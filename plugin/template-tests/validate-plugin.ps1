param([switch]$RequireFull)

$ErrorActionPreference = 'Stop'

$pluginRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $pluginRoot

$pluginJsonPath = Join-Path $pluginRoot '.claude-plugin/plugin.json'
if (-not (Test-Path $pluginJsonPath)) { throw "Missing $pluginJsonPath" }
$pluginJson = Get-Content $pluginJsonPath -Raw | ConvertFrom-Json
if ($pluginJson.name -ne 'agent-harness') { throw "plugin.json name must be 'agent-harness', got '$($pluginJson.name)'" }
if (-not $pluginJson.description) { throw 'plugin.json missing description' }

$marketplacePath = Join-Path $repoRoot '.claude-plugin/marketplace.json'
if (-not (Test-Path $marketplacePath)) { throw "Missing $marketplacePath" }
$marketplace = Get-Content $marketplacePath -Raw | ConvertFrom-Json
$entry = $marketplace.plugins | Where-Object { $_.name -eq 'agent-harness' }
if (-not $entry) { throw 'marketplace.json must list the agent-harness plugin' }
if ($entry.source -ne './plugin') { throw "marketplace entry source must be './plugin', got '$($entry.source)'" }

$skillsDir = Join-Path $pluginRoot 'skills'
$skillDirs = @()
if (Test-Path $skillsDir) { $skillDirs = @(Get-ChildItem $skillsDir -Directory) }
foreach ($dir in $skillDirs) {
    $skillPath = Join-Path $dir.FullName 'SKILL.md'
    if (-not (Test-Path $skillPath)) { throw "Missing SKILL.md in $($dir.FullName)" }
    $text = Get-Content $skillPath -Raw
    if ($text -notmatch ('(?s)^---.*?name:\s*' + [regex]::Escape($dir.Name) + '\b.*?---')) {
        throw "$($dir.Name)/SKILL.md frontmatter must declare 'name: $($dir.Name)'"
    }
    if ($text -notmatch 'description:\s*\S') { throw "$($dir.Name)/SKILL.md frontmatter missing a description" }
}

if ($RequireFull) {
    $required = @('new-dotnet-cli', 'new-dotnet-etl-api', 'new-expo-app', 'harness-update')
    foreach ($name in $required) {
        if (-not (Test-Path (Join-Path $skillsDir "$name/SKILL.md"))) { throw "Required skill missing: $name" }
    }
    $hooksPath = Join-Path $pluginRoot 'hooks/hooks.json'
    if (-not (Test-Path $hooksPath)) { throw "Missing $hooksPath" }
    $hooks = Get-Content $hooksPath -Raw | ConvertFrom-Json
    if (-not $hooks.hooks.PostToolUse) { throw 'hooks.json must register a PostToolUse hook' }
    foreach ($script in @('resolve-repo.ps1', 'write-stamp.ps1', 'harness-note.ps1', 'get-update.ps1', 'apply-update.ps1')) {
        if (-not (Test-Path (Join-Path $pluginRoot "scripts/$script"))) { throw "Required script missing: $script" }
    }
}

Write-Host 'Plugin validation passed.'
