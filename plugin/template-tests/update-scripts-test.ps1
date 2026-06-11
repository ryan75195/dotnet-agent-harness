$ErrorActionPreference = 'Stop'
if ($null -ne (Get-Variable -Name PSNativeCommandUseErrorActionPreference -Scope Global -ErrorAction SilentlyContinue)) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$pluginScripts = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts'
$work = Join-Path $env:TEMP 'agent-harness-update-test'
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory $work | Out-Null

function Assert([bool]$Cond, [string]$Msg) { if (-not $Cond) { throw "ASSERT FAILED: $Msg" } }

$repo = Join-Path $work 'harness-repo'
New-Item -ItemType Directory $repo | Out-Null
git -C $repo init -q -b main
git -C $repo config user.email t@t.local
git -C $repo config user.name t
$tpl = Join-Path $repo 'expo/templates/app'
New-Item -ItemType Directory (Join-Path $tpl '.githooks') -Force | Out-Null
New-Item -ItemType Directory (Join-Path $tpl 'src') -Force | Out-Null
Set-Content (Join-Path $tpl 'harness-manifest.json') '{ "ownedPaths": [".githooks/**", "CLAUDE.md", "harness-manifest.json"] }'
Set-Content (Join-Path $tpl '.githooks/pre-commit') 'echo AppTemplate v1'
Set-Content (Join-Path $tpl 'CLAUDE.md') '# AppTemplate docs v1'
Set-Content (Join-Path $tpl 'src/index.ts') 'export const appName = "AppTemplate";'
git -C $repo add .
git -C $repo commit -qm v1
$baseCommit = (git -C $repo rev-parse HEAD).Trim()

$proj = Join-Path $work 'MyApp'
robocopy $tpl $proj /E /NFL /NDL /NJH /NJS | Out-Null
$global:LASTEXITCODE = 0
Get-ChildItem $proj -Recurse -File | ForEach-Object {
    $c = [IO.File]::ReadAllText($_.FullName)
    $u = $c.Replace('AppTemplate', 'MyApp')
    if ($u -ne $c) { [IO.File]::WriteAllText($_.FullName, $u) }
}
$stampJson = @"
{
  "template": "expo-app",
  "stack": "expo",
  "repoUrl": "unused-in-tests",
  "templateDir": "expo/templates/app",
  "scaffoldCommit": "$baseCommit",
  "lastUpdateCommit": "$baseCommit",
  "renames": [
    { "from": "AppTemplate", "to": "MyApp" },
    { "from": "app-template", "to": "my-app" },
    { "from": "apptemplate", "to": "myapp" }
  ],
  "scaffoldedAt": "2026-06-11T00:00:00Z"
}
"@
Set-Content (Join-Path $proj '.harness.json') $stampJson

Add-Content (Join-Path $proj 'CLAUDE.md') 'project-specific notes'

Set-Content (Join-Path $tpl '.githooks/pre-commit') 'echo AppTemplate v2'
Set-Content (Join-Path $tpl '.githooks/post-commit') 'echo AppTemplate post'
Set-Content (Join-Path $tpl 'CLAUDE.md') '# AppTemplate docs v2'
Set-Content (Join-Path $tpl 'src/index.ts') 'export const appName = "AppTemplate v2";'
git -C $repo add .
git -C $repo commit -qm v2

$manifestPath = Join-Path $work 'update-manifest.json'
& (Join-Path $pluginScripts 'get-update.ps1') -ProjectDir $proj -RepoPath $repo -OutFile $manifestPath
$m = Get-Content $manifestPath -Raw | ConvertFrom-Json

Assert (@($m.files).Count -eq 3) "expected 3 candidates (src/index.ts is unowned), got $(@($m.files).Count)"
$pre = @($m.files) | Where-Object { $_.projectPath -eq '.githooks/pre-commit' }
Assert ($pre.classification -eq 'clean') "pre-commit should be clean, got '$($pre.classification)'"
$post = @($m.files) | Where-Object { $_.projectPath -eq '.githooks/post-commit' }
Assert ($post.classification -eq 'new') "post-commit should be new, got '$($post.classification)'"
$claude = @($m.files) | Where-Object { $_.projectPath -eq 'CLAUDE.md' }
Assert ($claude.classification -eq 'modified') "CLAUDE.md should be modified, got '$($claude.classification)'"
Assert ($m.headCommit -match '^[0-9a-f]{40}$') 'headCommit recorded'
Assert ($m.lastUpdateCommit -eq $baseCommit) 'lastUpdateCommit recorded'

& (Join-Path $pluginScripts 'apply-update.ps1') -ManifestPath $manifestPath
Assert ((Get-Content (Join-Path $proj '.githooks/pre-commit') -Raw) -match 'MyApp v2') 'clean file updated with rename applied'
Assert (Test-Path (Join-Path $proj '.githooks/post-commit')) 'new file added'
Assert ((Get-Content (Join-Path $proj '.githooks/post-commit') -Raw) -match 'MyApp post') 'new file renamed'
$claudeContent = Get-Content (Join-Path $proj 'CLAUDE.md') -Raw
Assert ($claudeContent -match 'project-specific notes') 'modified file left untouched'
Assert ($claudeContent -notmatch 'docs v2') 'modified file not overwritten'
Assert ((Get-Content (Join-Path $proj 'src/index.ts') -Raw) -match 'MyApp";') 'unowned src file untouched'

& (Join-Path $pluginScripts 'apply-update.ps1') -ManifestPath $manifestPath
Assert ((Get-Content (Join-Path $proj '.githooks/pre-commit') -Raw) -match 'MyApp v2') 'apply-update is idempotent'

Write-Host 'Update-script tests passed.'
Remove-Item -Recurse -Force $work
