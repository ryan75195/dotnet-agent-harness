#!/bin/sh
# Factory setup: point git at the gate hook below.
#
# The runner enables its gate only when this file exists, and then runs whatever
# core.hooksPath points at. With either missing it returns exit 0 without running
# anything, so an ungated repository is indistinguishable from one whose gate passed.
#
# The hook is committed executable. Do not chmod it here: that changes the file's mode
# relative to the index, and the planner's contract staging sweeps the difference in.
set -eu

REPO_ROOT="$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)"

git -C "$REPO_ROOT" config core.hooksPath .agent-factory/hooks

echo "[setup] factory gate wired (hooksPath=.agent-factory/hooks)"
exit 0
