. (Join-Path $PSScriptRoot 'register-with-factory-test-lib.ps1')
$fixture = New-RegistrationFixture
try {
    Invoke-Registration $fixture
    $before = Get-ChildItem -LiteralPath $fixture.Project -Recurse -File | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ Path = $_.FullName.Substring($fixture.Project.Length); Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
    Invoke-Registration $fixture
    $after = Get-ChildItem -LiteralPath $fixture.Project -Recurse -File | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ Path = $_.FullName.Substring($fixture.Project.Length); Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
    Assert (($before | ConvertTo-Json -Compress) -eq ($after | ConvertTo-Json -Compress)) 'AC-5 second registration changes no files'
} finally { Remove-RegistrationFixture $fixture }
Write-Host 'AC-5: passed'
