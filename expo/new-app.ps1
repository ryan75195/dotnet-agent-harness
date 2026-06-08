param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string]$Name,
    [string]$Destination = (Join-Path (Get-Location) $Name),
    [string]$BundleId = "com.example.$($Name.ToLower())"
)

$ErrorActionPreference = 'Stop'

$templateDir = Join-Path $PSScriptRoot 'templates\app'
if (Test-Path $Destination) {
    throw "Destination $Destination already exists."
}

Write-Host "Copying template to $Destination..."
robocopy $templateDir $Destination /E /XD node_modules .expo coverage .git /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) {
    throw "robocopy failed with exit code $LASTEXITCODE"
}
$global:LASTEXITCODE = 0

$slug = ($Name -creplace '(?<=[a-z0-9])(?=[A-Z])', '-').ToLower()
$lower = $Name.ToLower()
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

Write-Host "Renaming placeholders (name=$Name, slug=$slug, bundle=$BundleId)..."
Get-ChildItem $Destination -Recurse -File | ForEach-Object {
    $content = [IO.File]::ReadAllText($_.FullName)
    $updated = $content.
        Replace('com.example.apptemplate', $BundleId).
        Replace('AppTemplate', $Name).
        Replace('app-template', $slug).
        Replace('apptemplate', $lower)
    if ($updated -ne $content) {
        [IO.File]::WriteAllText($_.FullName, $updated, $utf8NoBom)
    }
}

Write-Host ""
Write-Host "Scaffolded $Name. Next steps:"
Write-Host "  cd $Destination"
Write-Host "  .\setup.ps1"
