#!/bin/bash

HARNESS_FEEDBACK_DIR="${AGENT_HARNESS_FEEDBACK_DIR:-$HOME/.agent-harness/feedback}"

harness_stamp_field() {
  local root field
  field="$1"
  root=$(git rev-parse --show-toplevel 2>/dev/null) || return 1
  [ -f "$root/.harness.json" ] || return 1
  sed -n 's/.*"'"$field"'"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$root/.harness.json" | head -1
}

harness_json_escape() {
  printf '%s' "$1" | tr -d '\r' | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g' -e 's/\t/\\t/g' | awk '{printf "%s\\n", $0}' | sed 's/\\n$//'
}

harness_log_failure() {
  (
    set +e
    local gate output root id ts branch template tail_text escaped project gitdir
    gate="$1"
    output="$2"
    root=$(git rev-parse --show-toplevel 2>/dev/null) || exit 0
    [ -f "$root/.harness.json" ] || exit 0
    mkdir -p "$HARNESS_FEEDBACK_DIR/diffs" 2>/dev/null || exit 0
    id=$(openssl rand -hex 3 2>/dev/null)
    [ -n "$id" ] || id=$(printf '%06x' $(( $(date +%s) % 16777216 )))
    ts=$(date -u +%Y-%m-%dT%H:%M:%SZ)
    branch=$(git branch --show-current 2>/dev/null)
    template=$(harness_stamp_field template)
    tail_text=$(printf '%s' "$output" | tail -n 50)
    escaped=$(harness_json_escape "$tail_text")
    project=$(harness_json_escape "$root")
    printf '{"id":"%s","kind":"gate-failure","ts":"%s","project":"%s","template":"%s","gate":"%s","branch":"%s","outputTail":"%s","note":null,"fixCommit":null}\n' \
      "$id" "$ts" "$project" "$template" "$gate" "$branch" "$escaped" \
      >> "$HARNESS_FEEDBACK_DIR/events.jsonl" 2>/dev/null || exit 0
    git diff --cached > "$HARNESS_FEEDBACK_DIR/diffs/$id.failure.patch" 2>/dev/null
    gitdir=$(git rev-parse --git-dir 2>/dev/null)
    [ -n "$gitdir" ] && echo "$id" > "$gitdir/harness-pending-event" 2>/dev/null
    echo "HARNESS-FEEDBACK: event $id logged - append a one-line note on what this code was trying to do."
  )
  return 0
}
