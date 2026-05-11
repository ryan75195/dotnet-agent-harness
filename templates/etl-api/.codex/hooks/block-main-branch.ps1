$branch = git branch --show-current 2>$null

if ($branch -eq "main" -or $branch -eq "master") {
    [Console]::Error.WriteLine("BLOCKED: file edits on $branch are not allowed.")
    [Console]::Error.WriteLine("Create a feat/<issue-num>-<slug> branch before editing.")
    exit 2
}

exit 0
