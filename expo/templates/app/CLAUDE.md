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

CI (`.github/workflows/ci.yml`) re-runs `npm run verify` on every PR and on
pushes to `main` — the same gate the pre-commit hook enforces, now on the
server.

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

- `src/app/` — expo-router routes (file-based). Thin glue: route files own
  router hooks and pass plain props to screens. Not coverage-gated.
- `src/components/` — shared presentational components. May import `lib`
  only.
- `src/features/<name>/` — screen UIs and feature logic, as pure props-driven
  components. Coverage-gated. Never import a sibling feature or `src/app`.
- `src/lib/` — platform/service wrappers (purchases, storage, api). Never
  imports app/features/components. The API client (`src/lib/api/`) wraps `fetch`
  and auto-attaches the auth bearer token; use `useApi()` in screens or
  `createApiClient(getToken)` elsewhere.
- dependency-cruiser enforces all of the above at error severity
  (`.dependency-cruiser.cjs`).
- Every module in `src/lib` and `src/features` must have a
  `__tests__/<name>.test.ts(x)` file (`scripts/check-test-files.js`) and
  coverage ≥ 80% lines/branches.
- Subscriptions: `src/lib/purchases/` wraps RevenueCat. The entitlement id
  is `PREMIUM_ENTITLEMENT` in `useSubscription.ts` and must match the
  RevenueCat dashboard.
- Auth: `src/lib/auth/` wraps Auth0 (OIDC via expo-auth-session) and native
  Apple Sign In. It is inert until `EXPO_PUBLIC_AUTH0_DOMAIN` and
  `EXPO_PUBLIC_AUTH0_CLIENT_ID` are set. `useAuth()` exposes `getToken()` —
  the single accessor for attaching a bearer token to your backend calls.
  `AuthProvider` (in `src/lib/auth/`) lifts `useAuth` once for the whole tree;
  `src/app/(app)/_layout.tsx` redirects to `/sign-in` when auth is enabled and
  the user is signed out (the config-gated login wall).
  When auth and payments are both on, `AuthProvider` calls `syncPurchasesIdentity`
  so RevenueCat entitlements follow the account.
  Account deletion (`useDeleteAccount`) calls `EXPO_PUBLIC_ACCOUNT_DELETE_URL`
  with the user's bearer token and then signs out; the backend performs the
  privileged Auth0 Management API delete (Apple 5.1.1(v)).

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
| 1 Local readiness | submission-doctor; auth-setup (if using auth) |
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
  build-and-submit, auth-setup
- `scripts/submission-doctor.js` — local readiness checks (`npm run doctor`)
- `.githooks/` + `.claude/hooks/` — lifecycle enforcement
- `eslint.config.js` + `eslint-rules/` — style enforcement
- `.dependency-cruiser.cjs` — layering enforcement
- `.github/workflows/ci.yml` — server-side guardrail enforcement on PRs

## Harness maintenance

This project was scaffolded from the agent-harness template repo.
`.harness.json` records the template and commit it came from — never
hand-edit or delete it.

- **Updating the harness.** On any request like "update the harness",
  "pull the latest template changes", or "update the guardrails", invoke
  the `agent-harness:harness-update` skill (ships with the agent-harness
  plugin). It updates only harness-owned files — hooks, lint rules,
  analyzers, CI, this file — never your app code, and goes through the
  normal issue → branch → PR lifecycle.
- **Feedback events.** When a blocked commit prints
  `HARNESS-FEEDBACK: event <id>`, append a one-line note describing what
  the failing code was trying to do (the agent-harness plugin injects the
  exact `harness-note.ps1` command), then fix the failure and commit
  again as normal.
