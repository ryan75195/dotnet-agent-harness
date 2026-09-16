$ErrorActionPreference = 'Stop'

$labels = [ordered]@{
    'factory:work-item'      = @{ Color = '1D76DB'; Description = 'Factory agent work item brief' }
    'factory:ready'          = @{ Color = '0E8A16'; Description = 'Approved for paid factory dispatch' }
    'factory:blocked'        = @{ Color = 'ededed'; Description = 'Waiting on a prerequisite ticket to merge' }
    'factory:running'        = @{ Color = 'ededed'; Description = 'A paid factory run is in progress' }
    'factory:awaiting-merge' = @{ Color = 'ededed'; Description = 'The factory opened a pull request; merge or reject it' }
    'factory:needs-human'    = @{ Color = 'ededed'; Description = 'The factory stopped and needs a maintainer' }
}

$origin = & git remote get-url origin 2>$null
$global:LASTEXITCODE = 0
if (-not $origin) {
    Write-Output 'No git remote named "origin"; skipping factory label creation.'
    exit 0
}

$ghCommand = Get-Command gh -ErrorAction SilentlyContinue
if (-not $ghCommand) {
    Write-Output 'gh CLI not found; skipping factory label creation.'
    exit 0
}

& gh auth status *> $null
if ($LASTEXITCODE -ne 0) {
    $global:LASTEXITCODE = 0
    Write-Output 'gh is not authenticated; skipping factory label creation.'
    exit 0
}
$global:LASTEXITCODE = 0

$existingNames = @()
$existingJson = & gh label list --json name 2>$null
if ($LASTEXITCODE -eq 0 -and $existingJson) {
    try {
        $existingNames = @(($existingJson | Out-String | ConvertFrom-Json) | ForEach-Object { $_.name })
    }
    catch {
        $existingNames = @()
    }
}
$global:LASTEXITCODE = 0

foreach ($name in $labels.Keys) {
    if ($existingNames -contains $name) {
        Write-Output "Label '$name' already exists, skipping."
        continue
    }

    $color = $labels[$name].Color
    $description = $labels[$name].Description
    & gh label create $name --color $color --description $description 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Output "Created label '$name'."
    }
    else {
        Write-Output "Could not create label '$name' (it may already exist)."
    }
    $global:LASTEXITCODE = 0
}

exit 0
