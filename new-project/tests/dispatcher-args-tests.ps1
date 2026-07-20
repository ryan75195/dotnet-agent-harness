$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$np = Join-Path $repo 'new-project.ps1'

function ExitCodeOf([string[]]$cliArgs) {
    # Windows PowerShell 5.1 note: under $ErrorActionPreference = 'Stop', redirecting
    # a native process's stderr (even to $null) wraps it in a terminating
    # NativeCommandError. Temporarily relax to 'Continue' around the call so we can
    # capture $LASTEXITCODE without the redirect itself throwing.
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $np @cliArgs *> $null
        return $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $prevEap }
}

$cases = @(
    @{ args = @();                                        expect = 0;  name = 'bare -> usage' },
    @{ args = @('-h');                                    expect = 0;  name = '-h -> usage' },
    @{ args = @('expo', 'MyApp', '-BundleId', 'com.a.b', '-DryRun'); expect = 0; name = 'dry-run' },
    @{ args = @('expo-tv', 'MyTvApp', '-BundleId', 'com.a.tv', '-DryRun'); expect = 0; name = 'tv dry-run' },
    @{ args = @('nope', 'Foo');                           expect = 2;  name = 'unknown type' },
    @{ args = @('expo');                                  expect = 2;  name = 'missing name' },
    @{ args = @('expo', 'lowerbad');                      expect = 2;  name = 'non-pascal name' },
    @{ args = @('dotnet-cli', 'Foo', '-BundleId', 'x');   expect = 2;  name = 'undeclared flag' },
    @{ args = @('expo', 'MyApp', '-BundleId');            expect = 2;  name = 'flag missing value' }
)
foreach ($c in $cases) {
    $got = ExitCodeOf $c.args
    if ($got -ne $c.expect) { throw "FAIL [$($c.name)]: expected exit $($c.expect), got $got" }
}

$stray = Join-Path (Get-Location) 'MyApp'
if (Test-Path $stray) { throw "dry-run/validation created a directory ($stray) - it must not" }

Write-Host 'dispatcher-args-tests: passed'

# The cases above deliberately run new-project.ps1 to a non-zero exit, so $LASTEXITCODE
# is 2 when the script ends. Under CI's `pwsh -command ". script.ps1"` that leaks into
# pwsh's exit code and fails the step despite every assertion passing. Exit explicitly.
# Any real failure throws under $ErrorActionPreference = 'Stop' and never reaches here.
exit 0
