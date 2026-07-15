$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repo 'new-project/lib.ps1')
$handlers = Get-NewProjectHandlers -HandlersDir (Join-Path $repo 'new-project/handlers')

function Assert($cond, $msg) { if (-not $cond) { throw "FAIL: $msg" } }
function AssertThrows($script, $match, $msg) {
    $threw = $false
    try { & $script } catch { $threw = $true; if ($match -and $_.Exception.Message -notmatch $match) { throw "FAIL: $msg - wrong error: $($_.Exception.Message)" } }
    if (-not $threw) { throw "FAIL: $msg - expected a throw" }
}

$types = ($handlers | ForEach-Object { $_.Type }) | Sort-Object
Assert (($types -join ',') -eq 'dotnet-cli,dotnet-durable,dotnet-etl-api,dotnet-mcp,expo') "discovery finds five types, got $($types -join ',')"

$usage = Format-NewProjectUsage -Handlers $handlers
foreach ($t in 'expo', 'dotnet-cli', 'dotnet-durable', 'dotnet-etl-api', 'dotnet-mcp') { Assert ($usage -match [regex]::Escape($t)) "usage lists $t" }
Assert ($usage -match 'BundleId') 'usage shows expo BundleId flag'

$plan = Resolve-NewProjectPlan -Handlers $handlers -Type 'expo' -Name 'MyApp' -Flags @{ BundleId = 'com.acme.myapp' } -Cwd 'C:/tmp'
Assert ($plan.Dest -eq (Join-Path 'C:/tmp' 'MyApp')) "expo default dest, got $($plan.Dest)"
Assert ($plan.Extra.BundleId -eq 'com.acme.myapp') 'expo carries BundleId'

$plan2 = Resolve-NewProjectPlan -Handlers $handlers -Type 'dotnet-cli' -Name 'MyTool' -Cwd 'C:/tmp'
Assert ($plan2.Extra.Count -eq 0) 'dotnet-cli has no extras'

AssertThrows { Resolve-NewProjectPlan -Handlers $handlers -Type 'nope' -Name 'X' -Cwd 'C:/tmp' } 'Unknown project type' 'unknown type errors'
AssertThrows { Resolve-NewProjectPlan -Handlers $handlers -Type 'expo' -Name '' -Cwd 'C:/tmp' } 'required' 'missing name errors'
AssertThrows { Resolve-NewProjectPlan -Handlers $handlers -Type 'expo' -Name 'lowerbad' -Cwd 'C:/tmp' } 'PascalCase' 'non-pascal name errors'
AssertThrows { Resolve-NewProjectPlan -Handlers $handlers -Type 'dotnet-cli' -Name 'X' -Flags @{ BundleId = 'y' } -Cwd 'C:/tmp' } 'not valid for type' 'undeclared flag errors'

$dry = Format-NewProjectPlan -Plan $plan2
Assert ($dry -match 'dotnet-cli') 'plan formats type'
Assert ($dry -match 'scaffold -> stamp') 'plan lists steps in order'

Write-Host 'lib-tests: all passed'
