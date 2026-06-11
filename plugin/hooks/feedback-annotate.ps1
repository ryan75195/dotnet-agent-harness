$ErrorActionPreference = 'SilentlyContinue'

$payload = [Console]::In.ReadToEnd()
if ($payload -match 'HARNESS-FEEDBACK: event ([0-9a-f]+)') {
    $eventId = $Matches[1]
    $notePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../scripts/harness-note.ps1'))
    $context = "A harness feedback event '$eventId' was just logged because a guardrail blocked this commit. " +
        "Before fixing the failure, append a one-line note describing what the failing code was trying to do: " +
        "powershell -NoProfile -File `"$notePath`" -EventId $eventId -Note `"<one-line description>`" " +
        "Then fix the failure and commit again."
    @{ hookSpecificOutput = @{ hookEventName = 'PostToolUse'; additionalContext = $context } } | ConvertTo-Json -Compress
}
exit 0
