$branch = git branch --show-current 2>$null

if ($branch -ne "main" -and $branch -ne "master") {
    exit 0
}

$payload = [Console]::In.ReadToEnd()
$commandText = $payload

try {
    $json = $payload | ConvertFrom-Json -ErrorAction Stop
    if ($null -ne $json.command) {
        $commandText = [string]$json.command
    } elseif ($null -ne $json.tool_input -and $null -ne $json.tool_input.command) {
        $commandText = [string]$json.tool_input.command
    }
} catch {
    $commandText = $payload
}

$mutatingPatterns = @(
    '\bSet-Content\b',
    '\bAdd-Content\b',
    '\bOut-File\b',
    '>\s*[^&\s]',
    '>>\s*[^&\s]',
    '\bNew-Item\b',
    '\bRemove-Item\b',
    '\bMove-Item\b',
    '\bRename-Item\b',
    '\bCopy-Item\b',
    '\bdel\b',
    '\berase\b',
    '\brm\b',
    '\bmv\b',
    '\bcp\b',
    '\bdotnet\s+format\b',
    '\bgit\s+add\b',
    '\bgit\s+commit\b'
)

foreach ($pattern in $mutatingPatterns) {
    if ($commandText -match $pattern) {
        [Console]::Error.WriteLine("BLOCKED: mutating shell command on $branch is not allowed.")
        [Console]::Error.WriteLine("Create or switch to a feat/<issue-num>-<slug> branch before editing.")
        exit 2
    }
}

exit 0
