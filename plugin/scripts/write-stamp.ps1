param(
    [Parameter(Mandatory = $true)][string]$ProjectDir,
    [Parameter(Mandatory = $true)][ValidateSet('cli', 'etl-api', 'expo-app')][string]$Template,
    [Parameter(Mandatory = $true)][string]$ProjectName,
    [Parameter(Mandatory = $true)][string]$RepoPath,
    [string]$BundleId
)

$ErrorActionPreference = 'Stop'

$templateDirs = @{
    'cli'      = 'dotnet/templates/cli'
    'etl-api'  = 'dotnet/templates/etl-api'
    'expo-app' = 'expo/templates/app'
}
$templateDir = $templateDirs[$Template]

$commit = (git -C $RepoPath rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw "Could not resolve HEAD of harness repo at $RepoPath" }

if ($Template -eq 'expo-app') {
    if (-not $BundleId) { $BundleId = "com.example.$($ProjectName.ToLower())" }
    $slug = ($ProjectName -creplace '(?<=[a-z0-9])(?=[A-Z])', '-').ToLower()
    # Build renames with Add() to avoid PS 5.1 case-insensitive duplicate-key parse error
    # (AppTemplate vs apptemplate would be rejected in a hash literal)
    $renames = [ordered]@{}
    $renames['com.example.apptemplate'] = $BundleId
    $renames['AppTemplate']             = $ProjectName
    $renames['app-template']            = $slug
    $renames['apptemplate']             = $ProjectName.ToLower()
    $stack = 'expo'
}
else {
    $templateJsonPath = Join-Path $RepoPath "$templateDir/.template.config/template.json"
    $sourceName = (Get-Content $templateJsonPath -Raw | ConvertFrom-Json).sourceName
    $renames = [ordered]@{ $sourceName = $ProjectName }
    $stack = 'dotnet'
}

$stamp = [ordered]@{
    template         = $Template
    stack            = $stack
    repoUrl          = 'https://github.com/ryan75195/dotnet-agent-harness'
    templateDir      = $templateDir
    scaffoldCommit   = $commit
    lastUpdateCommit = $commit
    renames          = $renames
    scaffoldedAt     = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
}

$json = $stamp | ConvertTo-Json -Depth 5
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[IO.File]::WriteAllText((Join-Path $ProjectDir '.harness.json'), $json, $utf8NoBom)
Write-Output "Wrote .harness.json (template=$Template, commit=$commit)"
