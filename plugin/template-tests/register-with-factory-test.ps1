#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'
$tests = @(
    'register-with-factory-ac1-test.ps1',
    'register-with-factory-ac2-test.ps1',
    'register-with-factory-ac3-test.ps1',
    'register-with-factory-ac4-test.ps1',
    'register-with-factory-ac5-test.ps1',
    'register-with-factory-ac6-test.ps1'
)
$failures = 0
foreach ($test in $tests) {
    try { & (Join-Path $PSScriptRoot $test) }
    catch { $failures++; Write-Error "$test failed: $($_.Exception.Message)" }
}
if ($failures -gt 0) { throw "$failures registration criterion test(s) failed" }
Write-Host 'register-with-factory-test: all passed'
