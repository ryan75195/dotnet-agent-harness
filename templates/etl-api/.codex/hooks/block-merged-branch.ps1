$branch = git branch --show-current 2>$null

if ($branch -notlike "feat/*") {
    exit 0
}

$gh = Get-Command gh -ErrorAction SilentlyContinue
if ($null -eq $gh) {
    exit 0
}

$mergedPr = gh pr list --head $branch --state merged --json number --jq '.[0].number' 2>$null
if ($LASTEXITCODE -ne 0) {
    exit 0
}

if (-not [string]::IsNullOrWhiteSpace($mergedPr)) {
    [Console]::Error.WriteLine("BLOCKED: $branch was already merged (PR #$mergedPr).")
    [Console]::Error.WriteLine("Start a new feat branch before editing.")
    exit 2
}

exit 0
