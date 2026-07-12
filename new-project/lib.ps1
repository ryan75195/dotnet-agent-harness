function Get-NewProjectHandlers {
    param([Parameter(Mandatory)][string]$HandlersDir)
    if (-not (Test-Path $HandlersDir)) { throw "Handlers dir not found: $HandlersDir" }
    $handlers = @()
    foreach ($file in Get-ChildItem -Path $HandlersDir -Filter '*.ps1' | Sort-Object Name) {
        $descriptor = & $file.FullName
        if (-not $descriptor -or -not $descriptor.Type) {
            throw "Handler $($file.Name) did not return a descriptor with a Type"
        }
        $handlers += $descriptor
    }
    return $handlers
}

function Format-NewProjectUsage {
    param([Parameter(Mandatory)][array]$Handlers)
    $lines = @('Usage: new-project <type> <Name> [flags]', '', 'Types:')
    foreach ($h in $Handlers) {
        $lines += "  $($h.Type)  -  $($h.Description)"
        $flagParts = @('-Destination <dir>')
        foreach ($a in @($h.ExtraArgs)) {
            $suffix = if ($a.Required) { '' } else { '?' }
            $flagParts += "-$($a.Name)$suffix <value>"
        }
        $lines += "      flags: $($flagParts -join '  ')"
    }
    $lines += @('', 'Name must be PascalCase (^[A-Z][A-Za-z0-9]*$).', 'Run with -DryRun to preview the plan without scaffolding.')
    return ($lines -join "`n")
}

function Resolve-NewProjectPlan {
    param(
        [Parameter(Mandatory)][array]$Handlers,
        [string]$Type,
        [string]$Name,
        [hashtable]$Flags = @{},
        [Parameter(Mandatory)][string]$Cwd
    )
    if (-not $Type) { throw 'No project type given. Run new-project with no args for usage.' }
    $handler = $Handlers | Where-Object { $_.Type -eq $Type } | Select-Object -First 1
    if (-not $handler) { throw "Unknown project type '$Type'. Known types: $((($Handlers | ForEach-Object { $_.Type })) -join ', ')." }
    if (-not $Name) { throw "Project name is required: new-project $Type <Name> [flags]" }
    if ($Name -cnotmatch '^[A-Z][A-Za-z0-9]*$') { throw "Name '$Name' must be PascalCase (^[A-Z][A-Za-z0-9]*$)." }

    $declared = @{}
    foreach ($a in @($handler.ExtraArgs)) { $declared[$a.Name] = $a }

    $extra = @{}
    foreach ($key in $Flags.Keys) {
        if ($key -eq 'Destination') { continue }
        if (-not $declared.ContainsKey($key)) { throw "Flag -$key is not valid for type '$Type'." }
        $extra[$key] = $Flags[$key]
    }
    foreach ($a in @($handler.ExtraArgs)) {
        if ($a.Required -and -not $extra.ContainsKey($a.Name)) { throw "Flag -$($a.Name) is required for type '$Type'." }
    }

    $dest = if ($Flags.ContainsKey('Destination') -and $Flags['Destination']) { $Flags['Destination'] } else { Join-Path $Cwd $Name }

    $steps = @()
    if ($handler.PreInstall) { $steps += 'pre-install templates' }
    $steps += @('scaffold', 'stamp provenance (.harness.json)', 'setup (git init, hooks, initial commit)', 'verify')

    return @{ Type = $Type; Handler = $handler; Name = $Name; Dest = $dest; Extra = $extra; Steps = $steps }
}

function Format-NewProjectPlan {
    param([Parameter(Mandatory)][hashtable]$Plan)
    $lines = @("Plan for new-project $($Plan.Type) $($Plan.Name):", "  destination: $($Plan.Dest)")
    foreach ($k in $Plan.Extra.Keys) { $lines += "  $($k): $($Plan.Extra[$k])" }
    $lines += "  steps: $($Plan.Steps -join ' -> ')"
    return ($lines -join "`n")
}
