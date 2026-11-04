#!/bin/bash

BRANCH=$(git branch --show-current 2>/dev/null)

if [[ "$BRANCH" == "main" || "$BRANCH" == "master" ]]; then
  echo "BLOCKED: file edits on $BRANCH are not allowed." >&2
  echo "Create a feat/<issue-num>-<slug> branch before editing." >&2
  exit 2
fi

exit 0
