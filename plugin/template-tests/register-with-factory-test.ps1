#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'
$tests = @(
    'register-with-factory-policy-file-test.ps1',
    'register-with-factory-setup-script-test.ps1',
    'register-with-factory-lint-script-test.ps1',
    'register-with-factory-no-contract-rules-test.ps1',
    'register-with-factory-rerun-test.ps1',
    'register-with-factory-unreachable-source-test.ps1'
)
$failures = 0
foreach ($test in $tests) {
    try { & (Join-Path $PSScriptRoot $test) }
    catch { $failures++; Write-Error "$test failed: $($_.Exception.Message)" }
}
if ($failures -gt 0) { throw "$failures registration criterion test(s) failed" }
Write-Host 'register-with-factory-test: all passed'
