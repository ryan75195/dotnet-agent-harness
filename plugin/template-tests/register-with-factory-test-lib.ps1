$ErrorActionPreference = 'Stop'

$script:repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$script:registrationScript = Join-Path $script:repo 'plugin/scripts/register-with-factory.ps1'

function Assert($condition, $message) {
    if (-not $condition) { throw "FAIL: $message" }
}

function New-RegistrationFixture {
    $root = Join-Path ([IO.Path]::GetTempPath()) ("factory-registration-" + [guid]::NewGuid().ToString('N'))
    $project = Join-Path $root 'project'
    $bin = Join-Path $root 'bin'
    New-Item -ItemType Directory -Path $project -Force | Out-Null
    New-Item -ItemType Directory -Path $bin -Force | Out-Null
    [pscustomobject]@{ Root = $root; Project = $project; Bin = $bin; OldPath = $env:PATH }
}

function Install-FakeGh {
    param(
        $fixture,
        [string]$Login = 'octocat',
        [switch]$Fail
    )
    $ghPath = Join-Path $fixture.Bin 'gh'
    if ($Fail) {
        [IO.File]::WriteAllText($ghPath, "#!/bin/sh`nexit 1`n")
    }
    else {
        [IO.File]::WriteAllText($ghPath, "#!/bin/sh`nif [ ""`$1"" = ""api"" ] && [ ""`$2"" = ""user"" ]; then echo $Login; exit 0; fi`nexit 1`n")
    }
    [IO.File]::SetUnixFileMode($ghPath, [IO.UnixFileMode]::UserExecute -bor [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite)
    $env:PATH = "$($fixture.Bin)$([IO.Path]::PathSeparator)$($fixture.OldPath)"
}

function Remove-FakeGh {
    param($fixture)
    $env:PATH = $fixture.OldPath
}

function Invoke-Registration($fixture) {
    & $script:registrationScript -ProjectDir $fixture.Project
}

function Remove-RegistrationFixture($fixture) {
    if ($fixture) { Remove-FakeGh $fixture }
    if ($fixture -and (Test-Path $fixture.Root)) { Remove-Item -Recurse -Force $fixture.Root }
}
