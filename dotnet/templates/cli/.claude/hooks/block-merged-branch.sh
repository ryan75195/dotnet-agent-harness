#!/bin/bash
# Shared check used by:
#   - Claude Code PreToolUse hook (Edit|Write|MultiEdit|NotebookEdit)
#   - .githooks/pre-commit (as a fail-fast first step)
#
# Exits 2 with a message on stderr when the current branch has already
# been merged via a closed PR — both Claude Code and git honor non-zero
# exit codes to abort the operation.
#
# Uses `gh pr list --state merged` so squash/rebase/normal merges are
# all detected (unlike `git branch --merged main` which misses squashes).

BRANCH=$(git branch --show-current 2>/dev/null)

# Only gate feat/* branches. main/master, fix/*, release/*, detached HEAD all pass through.
[[ "$BRANCH" == feat/* ]] || exit 0

# Needs gh + network. If either is unavailable, don't block — fail open.
command -v gh >/dev/null 2>&1 || exit 0

MERGED_PR=$(gh pr list --head "$BRANCH" --state merged --json number --jq '.[0].number' 2>/dev/null)

if [ -n "$MERGED_PR" ]; then
  echo "BLOCKED: $BRANCH was already merged (PR #$MERGED_PR)." >&2
  echo "Start a new feat branch — continuing to edit this one produces work that won't ship." >&2
  exit 2
fi

exit 0
