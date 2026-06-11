---
name: harness-capture-review
description: Use after a PR merges, or on request, to capture human review comments from a PR into the harness feedback store ("capture the review comments", "harvest PR feedback"). Routed from the merge step.
---

# Harness Capture Review

Capture **human** PR review comments into the feedback store as
`review-comment` events, so `harness-report` can later turn recurring
review feedback into new guardrails. Bots are skipped; capture is
idempotent (deduped on the GitHub comment id) and never blocks the merge
that preceded it.

## Flow

1. **Determine the PR.** Use the explicit PR number if given, else the PR
   for the current branch:
   ```powershell
   gh pr view --json number,url,author
   ```
   Resolve `owner/repo` with `gh repo view --json nameWithOwner`.

2. **Fetch human review data** (two sources):
   ```powershell
   gh api "repos/<owner>/<repo>/pulls/<pr>/comments" --paginate
   gh api "repos/<owner>/<repo>/pulls/<pr>/reviews" --paginate
   ```
   The first is inline comments (fields `id`, `user.login`, `user.type`,
   `path`, `line`, `body`, `diff_hunk`, `html_url`). The second is review
   summaries (use entries with a non-empty `body`; `user`, `state`,
   `html_url`).

3. **Normalize** both into one array where each element has: `commentId`
   (the numeric `id`), `authorType` (`user.type`), `author` (`user.login`),
   `isPrAuthor` (`true` when `user.login` equals the PR author), `body`,
   `file` (`path` or null for summaries), `line` (or null), `diffHunk`
   (`diff_hunk` or null), `commentUrl` (`html_url`). Write it to a temp
   JSON file.

4. **Capture:**
   ```powershell
   & "${CLAUDE_PLUGIN_ROOT}/scripts/capture-review.ps1" -InputJson <temp> -Pr <pr> -PrUrl <url>
   ```

5. **Report** the one-line summary the script prints. If any `gh` call
   fails (no PR, no auth, offline), say so and stop — do not retry in a
   loop and do not treat it as fatal to the surrounding work.
