$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'new-project/lib.ps1')
$handlers = Get-NewProjectHandlers -HandlersDir (Join-Path $PSScriptRoot 'new-project/handlers')

$argv = @($args)
$positional = @()
$flags = @{}
$dryRun = $false
$help = $false

try {
    $i = 0
    while ($i -lt $argv.Count) {
        $tok = $argv[$i]
        if ($tok -match '^(-h|--help|-Help)$') { $help = $true; $i++ }
        elseif ($tok -eq '-DryRun') { $dryRun = $true; $i++ }
        elseif ($tok -match '^-(Destination|Dest)$') { $flags['Destination'] = $argv[$i + 1]; $i += 2 }
        elseif ($tok -eq '-BundleId') { $flags['BundleId'] = $argv[$i + 1]; $i += 2 }
        elseif ($tok -like '-*') { throw "Unknown flag '$tok'." }
        else { $positional += $tok; $i++ }
    }

    if ($help -or $argv.Count -eq 0) {
        Write-Host (Format-NewProjectUsage -Handlers $handlers)
        exit 0
    }

    $type = $positional[0]
    $name = if ($positional.Count -ge 2) { $positional[1] } else { $null }
    $plan = Resolve-NewProjectPlan -Handlers $handlers -Type $type -Name $name -Flags $flags -Cwd (Get-Location).Path
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    [Console]::Error.WriteLine('')
    [Console]::Error.WriteLine((Format-NewProjectUsage -Handlers $handlers))
    exit 2
}

if ($dryRun) {
    Write-Host (Format-NewProjectPlan -Plan $plan)
    exit 0
}

$ctx = @{ Repo = $PSScriptRoot; Name = $plan.Name; Dest = $plan.Dest; Extra = $plan.Extra }
$handler = $plan.Handler

if (Test-Path $ctx.Dest) {
    [Console]::Error.WriteLine("Destination already exists: $($ctx.Dest)")
    exit 1
}

try {
    if ($handler.PreInstall) { & $handler.PreInstall $ctx }
    & $handler.Scaffold $ctx

    $stampArgs = @{ ProjectDir = $ctx.Dest; Template = $handler.StampName; ProjectName = $ctx.Name; RepoPath = $ctx.Repo }
    if ($ctx.Extra.ContainsKey('BundleId')) { $stampArgs['BundleId'] = $ctx.Extra['BundleId'] }
    & (Join-Path $PSScriptRoot 'plugin/scripts/write-stamp.ps1') @stampArgs

    Push-Location $ctx.Dest
    try {
        & (Join-Path $ctx.Dest 'setup.ps1')
        if ($LASTEXITCODE) { throw "setup.ps1 failed (exit $LASTEXITCODE) - see the message above (commonly a missing git identity)." }

        $stampStatus = git status --porcelain -- .harness.json
        if ($stampStatus) {
            git add .harness.json
            git commit --no-verify -q -m 'Add harness provenance stamp'
        }
        $global:LASTEXITCODE = 0

        & $handler.Verify $ctx
    }
    finally { Pop-Location }
}
catch {
    [Console]::Error.WriteLine("new-project failed: $($_.Exception.Message)")
    exit 1
}

Write-Host ''
Write-Host "Created $($plan.Type) project '$($ctx.Name)' at $($ctx.Dest)"
Write-Host 'Guardrails are active (see the project CLAUDE.md). Next: gh repo create, then follow the dev lifecycle.'
exit 0
