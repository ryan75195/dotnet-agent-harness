param(
    [Parameter(Mandatory = $true)][string]$ProjectDir,
    [Parameter(Mandatory = $true)][ValidateSet('cli', 'durable', 'etl-api', 'expo-app', 'mcp')][string]$Template,
    [Parameter(Mandatory = $true)][string]$ProjectName,
    [Parameter(Mandatory = $true)][string]$RepoPath,
    [string]$BundleId
)

$ErrorActionPreference = 'Stop'

$templateDirs = @{
    'cli'      = 'dotnet/templates/cli'
    'durable'  = 'dotnet/templates/durable'
    'etl-api'  = 'dotnet/templates/etl-api'
    'expo-app' = 'expo/templates/app'
    'mcp'      = 'dotnet/templates/mcp'
}
$templateDir = $templateDirs[$Template]

$commit = (git -C $RepoPath rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw "Could not resolve HEAD of harness repo at $RepoPath" }

if ($Template -eq 'expo-app') {
    if (-not $BundleId) { $BundleId = "com.example.$($ProjectName.ToLower())" }
    $slug = ($ProjectName -creplace '(?<=[a-z0-9])(?=[A-Z])', '-').ToLower()
    $renames = @(
        [ordered]@{ from = 'com.example.apptemplate'; to = $BundleId },
        [ordered]@{ from = 'AppTemplate'; to = $ProjectName },
        [ordered]@{ from = 'app-template'; to = $slug },
        [ordered]@{ from = 'apptemplate'; to = $ProjectName.ToLower() }
    )
    $stack = 'expo'
}
else {
    $templateJsonPath = Join-Path $RepoPath "$templateDir/.template.config/template.json"
    $sourceName = (Get-Content $templateJsonPath -Raw | ConvertFrom-Json).sourceName
    $renames = @([ordered]@{ from = $sourceName; to = $ProjectName })
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
