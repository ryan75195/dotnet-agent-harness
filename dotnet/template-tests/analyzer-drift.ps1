<#
.SYNOPSIS
Fails if the guardrail suite has drifted between the .NET templates.

.DESCRIPTION
Each template ships a self-contained copy of the shared analyzer suite and its
tests. That duplication is deliberate — a scaffolded project owns its guardrails
and depends on nothing — but it means a fix applied to one template silently
stays broken in the others. That is not hypothetical: the `<Clone>$` exclusion
in AnalyzerConstants (CI0002 demanding coverage of a record's compiler-generated
clone method, a diagnostic no one can satisfy) was fixed in `cli` and stranded
there for three templates.

This gate compares every file the templates share, normalised for the one thing
that legitimately differs between them: the root namespace. Anything else that
differs is drift, and drift is a bug.

Files unique to one template (durable's CI0016-CI0018 and their fixtures) are
reported and skipped — they are template-specific rules, not drift.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$templatesRoot = Join-Path $repoRoot 'templates'

$templates = [ordered]@{
    'cli'      = 'ConsoleApp'
    'etl-api'  = 'EtlApi'
    'mcp'      = 'SampleMcp'
    'durable'  = 'SampleDurable'
}

$suffixes = @('Analyzers', 'Tests.Analyzers')

function Get-SuiteDirectory {
    param([string]$Template, [string]$Namespace, [string]$Suffix)
    $sub = if ($Suffix -eq 'Analyzers') { 'src' } else { 'tests' }
    Join-Path $templatesRoot (Join-Path $Template (Join-Path $sub "$Namespace.$Suffix"))
}

function Get-Normalized {
    param([string]$Path, [string]$Namespace)
    $text = [System.IO.File]::ReadAllText($Path)
    $text = $text -replace [regex]::Escape($Namespace), '__NS__'
    $text = $text -replace "`r`n", "`n"
    $text.TrimStart([char]0xFEFF)
}

$reference = 'cli'
$drift = @()
$uniques = @()

foreach ($suffix in $suffixes) {
    $refDir = Get-SuiteDirectory -Template $reference -Namespace $templates[$reference] -Suffix $suffix
    if (-not (Test-Path $refDir)) { throw "Reference suite not found: $refDir" }

    $refFiles = Get-ChildItem $refDir -Filter '*.cs' -File | Select-Object -ExpandProperty Name

    foreach ($entry in $templates.GetEnumerator()) {
        $template = $entry.Key
        $ns = $entry.Value
        if ($template -eq $reference) { continue }

        $dir = Get-SuiteDirectory -Template $template -Namespace $ns -Suffix $suffix
        if (-not (Test-Path $dir)) { throw "Suite not found: $dir" }

        $files = Get-ChildItem $dir -Filter '*.cs' -File | Select-Object -ExpandProperty Name

        foreach ($name in $files | Where-Object { $refFiles -notcontains $_ }) {
            $uniques += "$template/$suffix/$name"
        }

        foreach ($name in $refFiles) {
            $theirs = Join-Path $dir $name
            if (-not (Test-Path $theirs)) {
                $drift += [pscustomobject]@{
                    File = "$suffix/$name"; Template = $template; Reason = 'MISSING'
                }
                continue
            }

            $a = Get-Normalized -Path (Join-Path $refDir $name) -Namespace $templates[$reference]
            $b = Get-Normalized -Path $theirs -Namespace $ns

            if ($a -ne $b) {
                $drift += [pscustomobject]@{
                    File = "$suffix/$name"; Template = $template; Reason = 'DIFFERS'
                }
            }
        }
    }
}

if ($uniques.Count -gt 0) {
    Write-Host "Template-specific files (not drift, skipped):" -ForegroundColor DarkGray
    $uniques | Sort-Object | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
    Write-Host ""
}

if ($drift.Count -eq 0) {
    Write-Host "analyzer-drift: the shared guardrail suite is identical across all $($templates.Count) templates" -ForegroundColor Green
    exit 0
}

Write-Host "analyzer-drift: FAILED - the shared guardrail suite has drifted" -ForegroundColor Red
Write-Host ""
foreach ($d in $drift | Sort-Object File, Template) {
    Write-Host ("  {0,-40} {1,-10} {2}" -f $d.File, $d.Template, $d.Reason) -ForegroundColor Red
}
Write-Host ""
Write-Host "Each template ships its own copy of the shared analyzers, so a fix applied to"
Write-Host "one must be applied to all. Reconcile the files above against '$reference' (or,"
Write-Host "if the change belongs in only one template, it is not part of the shared suite)."
Write-Host ""
Write-Host "To see a specific difference:"
Write-Host "  git diff --no-index dotnet/templates/$reference/src/ConsoleApp.Analyzers/<file> dotnet/templates/<t>/src/<Ns>.Analyzers/<file>"
exit 1
