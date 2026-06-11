$ErrorActionPreference = 'Stop'
if ($null -ne (Get-Variable -Name PSNativeCommandUseErrorActionPreference -Scope Global -ErrorAction SilentlyContinue)) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$pluginScripts = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts'
$work = Join-Path $env:TEMP 'agent-harness-stamp-test'
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory $work | Out-Null

function Assert([bool]$Cond, [string]$Msg) { if (-not $Cond) { throw "ASSERT FAILED: $Msg" } }

$repo = Join-Path $work 'repo'
New-Item -ItemType Directory $repo | Out-Null
git -C $repo init -q -b main
git -C $repo config user.email t@t.local
git -C $repo config user.name t
New-Item -ItemType Directory (Join-Path $repo 'dotnet/templates/cli/.template.config') -Force | Out-Null
Set-Content (Join-Path $repo 'dotnet/templates/cli/.template.config/template.json') '{ "sourceName": "ConsoleApp" }'
git -C $repo add .
git -C $repo commit -qm init
$sha = (git -C $repo rev-parse HEAD).Trim()

$proj = Join-Path $work 'proj'
New-Item -ItemType Directory $proj | Out-Null
& (Join-Path $pluginScripts 'write-stamp.ps1') -ProjectDir $proj -Template cli -ProjectName MyTool -RepoPath $repo
$stamp = Get-Content (Join-Path $proj '.harness.json') -Raw | ConvertFrom-Json
Assert ($stamp.template -eq 'cli') 'template field'
Assert ($stamp.stack -eq 'dotnet') 'stack field'
Assert ($stamp.templateDir -eq 'dotnet/templates/cli') 'templateDir field'
Assert ($stamp.scaffoldCommit -eq $sha) 'scaffoldCommit field'
Assert ($stamp.lastUpdateCommit -eq $sha) 'lastUpdateCommit field'
$r1 = @($stamp.renames)
Assert ($r1.Count -eq 1) 'one dotnet rename'
Assert ($r1[0].from -ceq 'ConsoleApp' -and $r1[0].to -ceq 'MyTool') 'dotnet sourceName rename'

$proj2 = Join-Path $work 'proj2'
New-Item -ItemType Directory $proj2 | Out-Null
& (Join-Path $pluginScripts 'write-stamp.ps1') -ProjectDir $proj2 -Template expo-app -ProjectName MyShinyApp -RepoPath $repo -BundleId com.example.myshinyapp
$stamp2 = Get-Content (Join-Path $proj2 '.harness.json') -Raw | ConvertFrom-Json
Assert ($stamp2.stack -eq 'expo') 'expo stack'
Assert ($stamp2.templateDir -eq 'expo/templates/app') 'expo templateDir'
$r2 = @($stamp2.renames)
Assert ($r2.Count -eq 4) 'four expo renames'
Assert ($r2[0].from -ceq 'com.example.apptemplate' -and $r2[0].to -ceq 'com.example.myshinyapp') 'bundle id rename first'
Assert ($r2[1].from -ceq 'AppTemplate' -and $r2[1].to -ceq 'MyShinyApp') 'name rename second'
Assert ($r2[2].from -ceq 'app-template' -and $r2[2].to -ceq 'my-shiny-app') 'slug rename third'
Assert ($r2[3].from -ceq 'apptemplate' -and $r2[3].to -ceq 'myshinyapp') 'lowercase rename fourth'
$raw2 = Get-Content (Join-Path $proj2 '.harness.json') -Raw
Assert ($raw2 -cmatch '"AppTemplate"') 'AppTemplate literal present in raw JSON'

$proj3 = Join-Path $work 'proj3'
New-Item -ItemType Directory $proj3 | Out-Null
& (Join-Path $pluginScripts 'write-stamp.ps1') -ProjectDir $proj3 -Template expo-app -ProjectName Plain -RepoPath $repo
$stamp3 = Get-Content (Join-Path $proj3 '.harness.json') -Raw | ConvertFrom-Json
Assert (@($stamp3.renames)[0].to -ceq 'com.example.plain') 'default bundle id'

$cfg = Join-Path $work 'config.json'
$escapedRepo = $repo -replace '\\', '\\'
Set-Content $cfg ('{ "repoPath": "' + $escapedRepo + '" }')
$resolved = & (Join-Path $pluginScripts 'resolve-repo.ps1') -ConfigPath $cfg | Select-Object -Last 1
Assert ($resolved -eq $repo) "resolve-repo should return configured checkout, got '$resolved'"

$cfgMissing = Join-Path $work 'config-missing.json'
Set-Content $cfgMissing '{ "repoPath": "C:/does/not/exist" }'
$failed = $false
try {
    & (Join-Path $pluginScripts 'resolve-repo.ps1') -ConfigPath $cfgMissing -NoClone | Out-Null
}
catch { $failed = $true }
Assert $failed 'resolve-repo with bad config and -NoClone must throw'

Write-Host 'Stamp tests passed.'
Remove-Item -Recurse -Force $work
