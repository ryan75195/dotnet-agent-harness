# PR Review-Comment Capture — Design

Date: 2026-06-11
Status: Approved pending spec review

## Overview

Extend the agent-harness feedback loop beyond pre-commit gate failures to
capture **human PR review comments** — the richest source of *new* rule
candidates, because a comment a reviewer had to leave is, by definition, a
rule the toolchain did not yet enforce. Captured comments feed
`harness-report`, which proposes the remedy (arch-test fixture,
dependency-cruiser rule, analyzer/ESLint rule).

Decided scope for THIS spec:

- Add a `kind` field to the event schema.
- A merge-time PR review-comment capture skill + testable script.
- `harness-report` changes to consume the new kind and propose remedies.

Explicitly out of scope (separate follow-ups, already agreed):

- Adding CI workflows to the dotnet templates (they ship none today; the
  expo template already has `.github/workflows/ci.yml`). CI *enforces* at
  merge but cannot capture (it runs on GitHub's servers with no access to
  the local `~/.agent-harness` store).
- User-correction capture (in-session "no, do it this way" feedback).

## Key decisions

- **Capture broadly, judge at report time.** The capture skill applies only
  mechanical filters (human-authored, substantive); the "is this
  rule-shaped?" judgment happens in `harness-report`. Matches the existing
  "capture dumb, judge later" philosophy of the gate-failure capture.
- **Human comments only.** Bot/app review comments (the repo's own
  code-review/ultrareview bots, linters) are skipped at capture — they
  would swamp the store. Author type `Bot`/app is excluded.
- **Merge-step routed, on-demand fallback.** The template CLAUDE.md
  lifecycle routes to the skill after `gh pr merge`; the skill also accepts
  an explicit PR number. Routing is soft (CC must follow it), acceptable
  because capture is best-effort and on-demand backstops it.
- **Script + thin skill seam.** Logic lives in a plain-PowerShell script
  that takes `gh` JSON as input, so it is testable offline with fixtures.
  The skill only runs `gh` and pipes to the script. Mirrors
  `harness-note.ps1` / `mark-reported.ps1`.
- **Dedup on the GitHub comment id.** Each event's id derives from the
  stable GitHub comment id, so re-running the skill on a PR never
  double-captures.

## Component 1 — Event schema: the `kind` field

Events today (gate-failure) look like:

```json
{"id":"a1b2c3","ts":"...","project":"...","template":"...","gate":"lint",
 "branch":"...","outputTail":"...","note":null,"fixCommit":null}
```

Change:

- The git-hook capture (`harness-feedback.sh`) adds `"kind":"gate-failure"`
  to each event it writes.
- Any consumer that folds the append-only store by `id` reads `kind` with a
  **default of `gate-failure`** when the field is absent — so events
  captured before this change still classify correctly. No migration of the
  existing store is performed.

`review-comment` events have this shape:

```json
{"id":"rc-1234567890","kind":"review-comment","ts":"2026-06-11T12:00:00Z",
 "project":"C:/.../my-app","template":"expo-app",
 "pr":42,"prUrl":"https://github.com/owner/my-app/pull/42",
 "commentUrl":"https://github.com/owner/my-app/pull/42#discussion_r1234567890",
 "commentId":1234567890,"author":"some-reviewer","file":"src/features/x/X.tsx",
 "line":17,"body":"this feature imports a sibling feature — route through lib",
 "note":null,"reportedIssue":null}
```

- `id` = `rc-<commentId>` (the GitHub comment id), giving idempotent dedup.
- Inline review comments carry `file`/`line`; review-summary bodies carry
  `file:null,line:null`.
- The diff hunk the comment was attached to (when present) is written to
  `~/.agent-harness/feedback/diffs/<id>.hunk.patch` — kept local, referenced
  by id, never inlined into reports (privacy, consistent with failure
  diffs).

## Component 2 — `capture-review.ps1`

`plugin/scripts/capture-review.ps1` — the testable core.

**Input:** a JSON array of PR comment objects (the shape produced by the
`gh` calls in Component 3), via `-InputJson` (a path) or stdin. Plus
`-ProjectDir` (default cwd, to read `.harness.json`) and `-Pr`/`-PrUrl`.

**Behaviour:**

1. Resolve the feedback store (`$env:AGENT_HARNESS_FEEDBACK_DIR` else
   `~/.agent-harness/feedback`); ensure `diffs/` exists.
2. Read existing `events.jsonl`, collect already-captured `commentId`s (fold
   by id; an event is "present" if any line has that id).
3. For each input comment, KEEP only if all hold:
   - author type is a human user (input field `authorType -eq 'User'`,
     not `Bot`/`Organization`/app);
   - it is substantive: body is non-empty after trimming, is not a pure
     approval token (`LGTM`, `:+1:`, `approved`, etc., case-insensitive
     exact/near match), and is not authored by the PR author
     (`isPrAuthor -eq $true` excluded);
   - its `commentId` is not already captured.
4. For each kept comment, append a `review-comment` event (schema above)
   and, if the comment has a `diffHunk`, write the hunk sidecar.
5. Print a one-line summary: `Captured N review comment(s) from PR <pr>
   (<skipped> skipped).`

**Robustness:** the script never throws on an individual malformed comment —
it skips it and continues (fail-open per-item), matching the capture
philosophy. A missing/!exist store dir is created. UTF-8 no-BOM writes via
`Add-Content`/`[IO.File]`.

## Component 3 — `harness-capture-review` skill

`plugin/skills/harness-capture-review/SKILL.md`. A thin orchestration:

1. Determine the PR: explicit `<pr>` argument, else the merged PR for the
   current branch (`gh pr view --json number,url,author`).
2. Fetch human review data with `gh`:
   - inline review comments:
     `gh api repos/{owner}/{repo}/pulls/{pr}/comments` (carries
     `user.login`, `user.type`, `path`, `line`, `body`, `id`, `diff_hunk`,
     `html_url`);
   - review summary bodies:
     `gh api repos/{owner}/{repo}/pulls/{pr}/reviews` (non-empty `body`,
     `state`, `user`).
3. Normalize both into the input array `capture-review.ps1` expects
   (`authorType` from `user.type`, `isPrAuthor` by comparing to the PR
   author, `commentId`, `file`/`line`, `body`, `diffHunk`, `commentUrl`),
   write it to a temp JSON file, and run:
   ```powershell
   & "${CLAUDE_PLUGIN_ROOT}/scripts/capture-review.ps1" -InputJson <temp> -Pr <pr> -PrUrl <url>
   ```
4. Report what was captured. Capture failures are reported but never block
   the surrounding workflow (the merge has already happened).

## Component 4 — Merge-step routing

Template CLAUDE.md "Development lifecycle" step 6 (`gh pr merge --squash`)
gains a follow-on line in all three templates: after a successful merge,
invoke `agent-harness:harness-capture-review` for the merged PR to capture
its human review comments. New projects bake this in; existing projects
receive it through `harness-update` (CLAUDE.md is a harness-owned file).

## Component 5 — `harness-report` changes

- When folding the store, read `kind` (default `gate-failure`).
- Cluster *within* a kind; do not mix gate-failures and review-comments in
  one cluster.
- For `review-comment` clusters, in addition to the existing digest fields,
  propose a **remedy type** by reading the comment bodies and file paths:
  - layering/structure/"imports sibling"/"belongs in lib|service" →
    arch-test fixture (.NET `tests/*.Architecture`) or dependency-cruiser
    rule (expo);
  - naming/style/"no any"/formatting → analyzer (CI####) or ESLint rule;
  - otherwise → CLAUDE.md guidance.
- Privacy unchanged: issue drafts summarize comment bodies and never paste
  raw diff hunks; the user approves every issue before posting.

## Error handling

- `capture-review.ps1` is fail-open per comment; a bad comment is skipped,
  not fatal. Store/dir creation is defensive.
- The skill's `gh` calls may fail (no PR, no auth, offline) — it reports the
  failure and stops without side effects; it never blocks the merge that
  preceded it.
- Re-running on the same PR is a no-op for already-captured comments
  (dedup by `commentId`).

## Testing

`plugin/template-tests/capture-review-test.ps1` (plain PowerShell, throws on
failure; wired into the `plugin-validate` CI job):

- Feed a fixture JSON array containing: two substantive human inline
  comments, one human review-summary body, one bot comment, one approval
  (`LGTM`) by a human, one comment by the PR author. Assert exactly the
  three substantive non-author human comments become `kind:review-comment`
  events, with correct `file`/`line`/`commentId`, and a hunk sidecar for the
  inline ones.
- Re-run with the same input and assert no new events (dedup on
  `commentId`).
- Back-compat: seed an events.jsonl line with NO `kind` field and a folding
  helper assertion that it classifies as `gate-failure`.
- (Existing `annotate-test.ps1` continues to cover note/mark-reported.)

`validate-plugin.ps1 -RequireFull` adds `harness-capture-review` to the
required skills list and `capture-review.ps1` to the required scripts list.

## Decisions log

- New event kind via a `kind` field; absence defaults to `gate-failure`
  (no store migration).
- Human comments only; bots excluded at capture.
- Capture broad, judge at report; remedy-type proposal lives in
  harness-report.
- Merge-routed (soft) + on-demand; capture never blocks the merge.
- Script+skill seam for offline testability; dedup on GitHub comment id.
