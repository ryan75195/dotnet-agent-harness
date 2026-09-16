#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'
$tests = @(
    'register-with-factory-policy-file-test.ps1',
    'register-with-factory-placeholder-test.ps1',
    'register-with-factory-setup-script-test.ps1',
    'register-with-factory-no-lint-script-test.ps1',
    'register-with-factory-rerun-test.ps1',
    'register-with-factory-preserves-existing-policy-test.ps1'
)
$failures = 0
foreach ($test in $tests) {
    try { & (Join-Path $PSScriptRoot $test) }
    catch { $failures++; Write-Error "$test failed: $($_.Exception.Message)" }
}
if ($failures -gt 0) { throw "$failures registration criterion test(s) failed" }
Write-Host 'register-with-factory-test: all passed'
