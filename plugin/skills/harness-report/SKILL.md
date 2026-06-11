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

Fold the append-only store to current-state-per-id with the helper (it
defaults a missing `kind` to `gate-failure` for back-compat):
```powershell
& "${CLAUDE_PLUGIN_ROOT}/scripts/fold-events.ps1" | ConvertFrom-Json
```
Skip events that already have `reportedIssue`. If nothing is unreported,
say so and stop.

### 2. Cluster (within a kind, never across)

Group unreported events by recurring mistake, clustering **within a single
`kind`** — never mix `gate-failure` and `review-comment` in one cluster.

- `gate-failure`: same gate + template + similar failure text/note. Read
  the `diffs/<id>.failure.patch` / `.fix.patch` sidecars when the
  outputTail is not enough.
- `review-comment`: same template + similar guidance across PRs/files. Read
  the `diffs/<id>.hunk.patch` sidecar for the code the comment was on.
  These are the highest-value clusters — a comment a human reviewer had to
  leave is a rule the toolchain did not enforce.

For each `review-comment` cluster, propose a **remedy type** from the
comment bodies and file paths:
- layering/structure ("imports a sibling", "belongs in lib/service",
  dependency direction) → an arch-test fixture (.NET
  `tests/*.Architecture`) or a dependency-cruiser rule (expo);
- naming/style ("no any", "name by intent", formatting) → an analyzer
  (CI####) or an ESLint rule;
- otherwise → CLAUDE.md guidance.

Singletons are normally not worth reporting — mention them and let the user
decide.

### 3. Draft and confirm — never post without showing the user

For each cluster, draft an issue body containing ONLY: occurrence count,
gate, template(s), project directory NAMES (not full paths), event ids,
the one-line notes, and a one-paragraph hypothesis of the rule that would
have prevented it. NO raw diffs, NO output tails, NO code — the harness
repo is public and events may come from private projects; diffs stay on
disk, referenced by event id. For `review-comment` clusters, also include
the proposed remedy type and the reviewer guidance summarized in your own
words — never paste raw diff hunks (sidecars stay local; the harness repo
is public). Show the user every title+body and get approval before posting
anything.

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
