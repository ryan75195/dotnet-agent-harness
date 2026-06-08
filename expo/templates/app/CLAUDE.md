# CLAUDE.md

Project context for Claude Code sessions. Read this before making changes.

## Development lifecycle

Every change follows this loop. None of these steps are optional — hooks
enforce each transition.

1. **Open an issue.** `gh issue create --title "..."`. No issue, no branch.
2. **Create a feat branch.** `git checkout -b feat/<N>-<kebab-slug>` where
   `<N>` is the issue number. `.githooks/reference-transaction` rejects the
   branch if the name doesn't match or issue #N doesn't exist.
3. **Edit + test.** `npm run verify` runs the full guardrail set locally.
4. **Commit.** `.githooks/pre-commit` runs: branch guard, merged-branch
   check, `tsc --noEmit`, `eslint . --max-warnings 0`, dependency-cruiser,
   check-test-files, `jest --coverage`. Any failure blocks the commit.
5. **Open PR.** `gh pr create --base main --head feat/<N>-<slug>`.
6. **Squash merge.** `gh pr merge <N> --squash --delete-branch`.

Direct edits and commits to `main` are blocked. Edits to an already-merged
branch are blocked (Claude Code PreToolUse hooks + pre-commit).

## Code style

- **No comments.** `local/no-comments` fires at error severity. Extract
  intent into function, variable, or type names.
- **No inline rule escapes.** ESLint runs with `noInlineConfig` —
  `eslint-disable` comments do nothing (and are themselves lint errors).
- **No `any`.** `@typescript-eslint/no-explicit-any` at error severity.
- **Caps:** 60 lines per function, 4 parameters, 300 lines per file, one
  exported component per file.
- **Strict TypeScript:** `strict`, `noUncheckedIndexedAccess`,
  `noImplicitOverride`.

## Architecture

- `src/app/` — screens and navigation. May import anything.
- `src/components/` — shared presentational components. May import `lib`
  only.
- `src/features/<name>/` — vertical feature slices. Never import a sibling
  feature.
- `src/lib/` — platform/service wrappers (purchases, storage, api). Never
  imports app/features/components.
- dependency-cruiser enforces all of the above at error severity
  (`.dependency-cruiser.cjs`).
- Every module in `src/lib` and `src/features` must have a
  `__tests__/<name>.test.ts(x)` file (`scripts/check-test-files.js`) and
  coverage ≥ 80% lines/branches.
- Subscriptions: `src/lib/purchases/` wraps RevenueCat. The entitlement id
  is `PREMIUM_ENTITLEMENT` in `useSubscription.ts` and must match the
  RevenueCat dashboard.

## App Store submission — orchestration

`SUBMISSION.md` is the single source of truth for submission state. On ANY
submission-related request ("set up the app store", "create the products",
"submit the app", "are we ready?"):

1. **Read `SUBMISSION.md` first.** Find the first stage with unchecked
   items — that is the current stage. Never skip ahead.
2. **Invoke the skill that owns the stage:**

| Stage | Owner |
|---|---|
| 0 Prerequisites | human (verify, don't automate) |
| 1 Local readiness | submission-doctor |
| 2 ASC app record | asc-setup |
| 3 Subscription products | asc-setup |
| 4 RevenueCat | revenuecat-setup |
| 5 Build & submit | build-and-submit |
| 6 App Review | build-and-submit / asc-setup (status) |

3. **Update `SUBMISSION.md` after every completed step** — check boxes,
   record IDs/values. Commit SUBMISSION.md changes through the normal dev
   lifecycle.
4. **Secrets never enter the repo.** `.p8` keys, demo account passwords,
   and API keys live outside git (`.env.*` files are gitignored; `*.p8` is
   gitignored as a backstop).
5. **Stop at human gates.** Login/2FA screens, App Privacy answers, pricing
   decisions, and review screenshots need the user — pause and ask.

## Key files

- `SUBMISSION.md` — submission state machine
- `.claude/skills/` — submission-doctor, asc-setup, revenuecat-setup,
  build-and-submit
- `scripts/submission-doctor.js` — local readiness checks (`npm run doctor`)
- `.githooks/` + `.claude/hooks/` — lifecycle enforcement
- `eslint.config.js` + `eslint-rules/` — style enforcement
- `.dependency-cruiser.cjs` — layering enforcement
