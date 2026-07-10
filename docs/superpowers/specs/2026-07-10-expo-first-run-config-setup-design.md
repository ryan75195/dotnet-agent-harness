# Expo first-run config setup — design

Date: 2026-07-10
Status: Approved (design), pending implementation plan

## Problem

Scaffolded Expo apps (`expo/templates/app`) build and run in dev immediately, but
reaching a *buildable and deployable* state requires config that is easy to forget:
EAS login, an EAS `projectId`, a real bundle identifier, and iOS credentials. In the
current app this has been a recurring pain point — a build is attempted and only then
hits the login/`eas init`/credentials walls.

We want a mechanism that, at the start of every Claude Code session in a scaffolded
project, detects missing critical config and drives an interview to fill it, so the
app can be built and deployed on request without surprises. Optional feature config
(payments, auth) is offered but never forced.

## Goals

- Detect missing build/deploy-critical config at session start and nudge Claude to
  fix it immediately.
- Make EAS "ready to build & deploy" the headline outcome: logged in, project linked,
  production profiles present, iOS credentials provisioned.
- Offer optional features (payments, auth) once and remember the decision.
- Stay silent and cheap on healthy sessions.
- Reuse existing template idioms (bash hooks shelling into Node scripts, dual-write of
  `EXPO_PUBLIC_*` to `.env.local` + `.env.production`, issue→branch→PR lifecycle).

## Non-goals

- Store-submission metadata (app icon, splash image, privacy/support URLs). These stay
  owned by the existing `submission-doctor` / `SUBMISSION.md` flow. This design keeps a
  clean build-time vs submit-time separation.
- Replacing or folding in `submission-doctor.js`.
- Android build/deploy readiness (template targets iOS first; not in scope here).

## Architecture

Three cooperating pieces, mirroring the template's existing "thin bash hook → Node
script" split (as `block-main-branch.sh` and `submission-doctor.js` already do):

1. **`.claude/hooks/session-config-check.sh`** — a logic-free bash wrapper wired to the
   `SessionStart` hook event in `.claude/settings.json`. It runs the Node detector and
   passes its stdout through unchanged (Claude Code injects hook stdout as session
   context). Exits 0 always (a detector problem must never break session start).

2. **`scripts/config-doctor.js`** — the Node detector. Sibling to `submission-doctor.js`,
   reusing its table-of-checks style. Inspects live config state and prints:
   - nothing (or a single `config OK` line) when all critical items pass and every
     optional feature has a recorded decision;
   - otherwise a short status block listing what is set / missing / deferred, followed
     by an instruction telling Claude to immediately run the `first-run-setup` skill.

   The detector is the source of the nudge; it does not itself write config.

3. **`.claude/skills/first-run-setup/SKILL.md`** — the interviewer. Claude follows it to
   run the actual Q&A (interactive question tool), execute and verify EAS commands, and
   write answers into `app.config.js`, `.env.local`, and `.env.production`.

The hook detects and nudges (it cannot be interactive); the skill does the interview and
the writes.

### Rejected alternatives

- **Pure-bash detector** — rejected; parsing `app.config.js`/`eas.json`/JSON in bash is
  fragile. Node matches `submission-doctor.js`.
- **Fold detection into `submission-doctor.js`** — rejected; doctor is submit-time and
  store-focused. A separate `config-doctor` preserves the build-time vs submit-time
  boundary.
- **Hook only prints status, never auto-triggers** — rejected; the pain point is
  *forgetting*, so the detector must actively nudge Claude to run the interview.
- **No hook, manual `/setup` skill only** — rejected; nothing would remind at session
  start.

## Config tiers

### Tier 1 — Critical (blocks build & deploy; re-verified live every session)

- `eas whoami` succeeds (logged in).
- `extra.eas.projectId` present in `app.config.js`.
- `eas.json` has `build.production` and `submit.production` profiles (already true in
  the template; checked to catch regressions).
- Production bundle identifier customised (not `com.example.*`).
- iOS credentials provisioned (verified via `eas credentials`).

Tier 1 is **never** marked "done" in persisted state. It is always re-verified from live
facts, which is what makes "buildable right now" trustworthy. If any Tier 1 item fails,
the detector nudges every session until it passes.

### Tier 2 — Optional features (offered once; decision remembered)

- Payments: `EXPO_PUBLIC_REVENUECAT_IOS_API_KEY`.
- Auth: `EXPO_PUBLIC_AUTH0_DOMAIN` + `EXPO_PUBLIC_AUTH0_CLIENT_ID`
  (+ `EXPO_PUBLIC_ACCOUNT_DELETE_URL` required when auth is enabled — Apple 5.1.1(v)).
- `EXPO_PUBLIC_API_BASE_URL`.

Each Tier 2 feature is offered once. The user's choice (`enabled` / `deferred`) is
recorded in state and not re-nagged. If the user later wants a deferred feature, they can
re-run the skill.

### Out of scope (owned by submission-doctor)

App icon, splash image, privacy/support URLs.

## Detector behaviour

`config-doctor.js` reads:

- `app.config.js` (production env, as `submission-doctor.js` does) for `extra.eas.projectId`
  and `ios.bundleIdentifier`;
- `eas.json` for production build/submit profiles;
- `.env.production` / `.env.local` for `EXPO_PUBLIC_*` values;
- `.claude/.setup-state.json` for Tier 2 decisions and schema version;
- **live `eas whoami`** with a ~3s timeout (see EAS check cost below).

Output contract:

- **Silent (exit 0, no nudge)** when: all Tier 1 checks pass AND every Tier 2 feature has
  a recorded decision.
- **Nudge (exit 0, stdout block)** otherwise: a compact status list plus an instruction
  for Claude to run the `first-run-setup` skill now.

The detector never exits non-zero and never blocks the session — a broken or offline
detector degrades to a best-effort nudge, not a failure.

### EAS check cost — live `eas whoami` with timeout

The detector runs `eas whoami` live each session, wrapped in a ~3s timeout. On
timeout/offline/error it degrades gracefully: it treats login state as *unknown* and
emits a "verify EAS login" nudge rather than a false "not logged in", and never hangs
session start. This keeps every session accurate about login state (the most common
regression) at a bounded latency cost.

## Interview behaviour (first-run-setup skill)

Claude works Tier 1 first, then offers Tier 2. Per item:

- **Not logged in** → ask the user to run `! eas login`, then re-check `eas whoami`.
- **No `projectId`** → run `eas init` (or `! eas init` if it needs interactive auth), then
  commit the `app.config.js` change through the normal issue→branch→PR lifecycle described
  in `CLAUDE.md`.
- **Placeholder bundle ID** → ask for the real bundle identifier; write it into
  `app.config.js`.
- **iOS credentials** → drive `! eas credentials` (needs Apple Developer login). If Apple
  Developer membership / App Store Connect access is not in place, report it as a blocker
  and stop that step rather than looping. (Stage 0 prerequisites in `SUBMISSION.md`.)
- **Tier 2 features** → for each, ask "enable now / skip for now". On enable, collect the
  key(s) and write them to both `.env.local` and `.env.production` (mirroring the
  `revenuecat-setup` dual-write). On skip, record `deferred`. When auth is enabled, also
  require `EXPO_PUBLIC_ACCOUNT_DELETE_URL` and confirm the `expo-apple-authentication`
  plugin is present (consistent with `submission-doctor` rules).

After a successful Tier 1 pass, the skill records the outcome so subsequent sessions are
quiet unless a live check regresses.

## State & idempotency

- **File:** `.claude/.setup-state.json`, added to `.gitignore` (per-developer-machine
  state: EAS login and credentials are machine/account-level, and deferral decisions are
  local preferences, so this is not committed).
- **Contents:** a schema `version` and a map of Tier 2 feature → decision
  (`enabled` | `deferred`), plus optional timestamps.
- Tier 1 is intentionally absent from state — always re-verified live.
- Schema `version` lets a future detector invalidate stale state formats safely.

## Testing

- `config-doctor.js` gets Jest coverage in the template's existing style: table-driven
  cases over synthesized `app.config.js` / `eas.json` / `.env*` / `.setup-state.json`
  fixtures, asserting the three output states (critical-missing nudge, optional-deferred
  quiet, all-clear silent).
- The `eas` CLI is shelled out; the detector's `eas whoami` call is injected/mocked in
  tests (e.g. via an overridable runner or PATH shim) so tests are hermetic and never hit
  the network. Timeout and offline-degradation paths are covered.
- The bash hook wrapper is logic-free, so there is nothing to unit-test there; the
  existing `template-tests/scaffold-and-validate.ps1` can assert the hook is wired in
  `.claude/settings.json` and the files exist.

## Template integration

- New files land under `expo/templates/app/`:
  - `.claude/hooks/session-config-check.sh`
  - `scripts/config-doctor.js` (+ `scripts/__tests__` or co-located Jest test)
  - `.claude/skills/first-run-setup/SKILL.md`
- `.claude/settings.json` gains a `SessionStart` hook entry invoking
  `bash .claude/hooks/session-config-check.sh`.
- `.gitignore` gains `.claude/.setup-state.json`.
- `harness-manifest.json` already covers `.claude/**` and `scripts/**` — no change needed.
- `new-app.ps1` placeholder rename needs no change (no new placeholders introduced).
- `README.md` / `CLAUDE.md`: a short note that session start runs a config check and may
  launch the first-run-setup interview.

## Success criteria

- On a freshly scaffolded project, the first session surfaces a nudge and, on running the
  skill, reaches EAS-ready (logged in, `projectId` set, production profiles, iOS
  credentials) and a real bundle ID — after which `eas build` / `eas submit` run without
  hitting setup walls.
- Optional features are offered once and not re-nagged after a decision.
- A fully configured project starts sessions silently.
