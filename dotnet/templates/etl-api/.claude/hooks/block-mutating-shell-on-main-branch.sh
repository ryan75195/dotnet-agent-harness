#!/bin/bash

BRANCH=$(git branch --show-current 2>/dev/null)

if [[ "$BRANCH" != "main" && "$BRANCH" != "master" ]]; then
  exit 0
fi

PAYLOAD=$(cat)
COMMAND_TEXT="$PAYLOAD"

if command -v jq >/dev/null 2>&1; then
  PARSED_COMMAND=$(printf '%s' "$PAYLOAD" | jq -r '.tool_input.command // .command // empty' 2>/dev/null)
  if [[ -n "$PARSED_COMMAND" ]]; then
    COMMAND_TEXT="$PARSED_COMMAND"
  fi
else
  PARSED_COMMAND=$(printf '%s' "$PAYLOAD" | sed -nE 's/.*"command"[[:space:]]*:[[:space:]]*"([^"]*)".*/\1/p' | head -1)
  if [[ -n "$PARSED_COMMAND" ]]; then
    COMMAND_TEXT="$PARSED_COMMAND"
  fi
fi

MUTATING_PATTERN='(^|[[:space:];|&])(cat[[:space:]].*>|tee[[:space:]]|sed[[:space:]].*-i|python[[:space:]].*open\(.*["'\'']w|dotnet[[:space:]]+format|git[[:space:]]+add|git[[:space:]]+commit|rm|mv|cp|del|erase|Set-Content|Add-Content|Out-File|New-Item|Remove-Item|Move-Item|Rename-Item|Copy-Item)([[:space:];|&]|$)|(^|[^&])>[[:space:]]*[^&[:space:]]|>>[[:space:]]*[^&[:space:]]'

if [[ "$COMMAND_TEXT" =~ $MUTATING_PATTERN ]]; then
  echo "BLOCKED: mutating shell command on $BRANCH is not allowed." >&2
  echo "Create or switch to a feat/<issue-num>-<slug> branch before editing." >&2
  exit 2
fi

exit 0
