---
name: harness-report
description: Use when asked to report harness feedback, promote captured guardrail failures into issues, or check "anything worth turning into a rule?". Clusters unreported feedback events and raises digest issues on the agent-harness repo.
---

# Harness Report

Promote captured feedback events into digest GitHub issues on the harness
repo — one issue per recurring failure pattern, so each can become a new
guardrail. Capture is local and automatic; reporting is batched, reviewed
by the user, and explicit.

## Flow

### 1. Load the store

Read `~/.agent-harness/feedback/events.jsonl` (or
`$env:AGENT_HARNESS_FEEDBACK_DIR/events.jsonl`). The store is append-only:
later lines with the same `id` are patches (note, fixCommit,
reportedIssue). Fold to the latest state per id. Skip events that already
have `reportedIssue`. If nothing is unreported, say so and stop.

### 2. Cluster

Group the unreported events by recurring mistake, not just by gate: same
gate + same template + similar failure text/note usually means one
candidate rule. Read the `diffs/<id>.failure.patch` / `.fix.patch`
sidecars locally when the outputTail is not enough to characterize a
cluster. Singletons are normally not worth reporting — mention them to
the user and let them decide.

### 3. Draft and confirm — never post without showing the user

For each cluster, draft an issue body containing ONLY: occurrence count,
gate, template(s), project directory NAMES (not full paths), event ids,
the one-line notes, and a one-paragraph hypothesis of the rule that would
have prevented it. NO raw diffs, NO output tails, NO code — the harness
repo is public and events may come from private projects; diffs stay on
disk, referenced by event id. Show the user every title+body and get
approval before posting anything.

### 4. Post and mark

For each approved cluster:
```powershell
gh issue create --repo ryan75195/dotnet-agent-harness --title "<short pattern summary>" --body "<digest>"
```
Then mark every event in the cluster:
```powershell
& "${CLAUDE_PLUGIN_ROOT}/scripts/mark-reported.ps1" -EventId <id> -IssueUrl <url>
```

### 5. Summarize

Report: issues raised (links), clusters skipped and why, events remaining
unreported.
