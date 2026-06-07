# Expo Agent Harness — Design

**Date:** 2026-06-07
**Status:** Approved

## Goal

Extend the agent-harness concept beyond .NET: a template Expo project plus Claude skills that take an app from `setup.ps1` to a submitted iOS App Store build, with the same error-severity guardrail philosophy as the dotnet templates. The submission gauntlet observed on journal-app (ASC app record, subscription products, RevenueCat, EAS build/submit, review metadata) becomes a staged, resumable, agent-driven workflow orchestrated by CLAUDE.md.

## Scope decisions

| Decision | Choice |
|---|---|
| Scope | Full zero-to-submission (scaffold + submission automation) |
| Account-level automation | Claude skills drive Playwright for browser-only steps; CLI (EAS) elsewhere |
| Monetization | RevenueCat subscriptions + paywall baked into every scaffold |
| Guardrails | Full port of the dotnet error-severity discipline |
| Packaging | This repo repurposed into a multi-language harness monorepo |
| Platforms | iOS App Store in v1; Google Play skills are a follow-up. Template stays cross-platform. |

## 1. Repo restructure

```
dotnet-agent-harness/                (candidate rename: agent-harness)
  README.md                          ← rewritten: multi-language harness index
  .github/workflows/template-ci.yml  ← two jobs: dotnet, expo
  dotnet/
    templates/cli/
    templates/etl-api/
    template-tests/scaffold-and-build.ps1
  expo/
    templates/app/
    template-tests/scaffold-and-validate.ps1
```

- Pure move for the dotnet side. `dotnet new install` scans recursively, so installing from the repo root or `.\dotnet` registers both templates unchanged.
- CI workflow paths and README install instructions update to the new layout.
- Repo rename to `agent-harness` is optional and out of band (GitHub redirects old clones).

## 2. Expo template (`expo/templates/app/`)

Scaffolding mechanism: `setup.ps1` clone-and-rename, matching the dotnet style — no npm publishing. The script string-replaces the app name into `app.config.js`, `package.json`, and the iOS bundle identifier, initialises git on `main`, activates `.githooks/`, and commits the scaffold.

Contents:

- `app.config.js` and `eas.json` with `development` / `preview` / `production` build profiles, `autoIncrement: true`, `appVersionSource: remote` — modeled on journal-app's converged configuration.
- `src/` layout enforced by dependency-cruiser: `app/` (screens/navigation), `features/`, `components/`, `lib/`.
- RevenueCat baked in: `react-native-purchases` wired in `lib/purchases/`, an entitlement-gated `useSubscription` hook, and a pre-built paywall screen.
- `.env.example` plus env handling, jest + jest-expo config, `.nvmrc`.

## 3. Guardrail layer

Everything fires at error severity — enforced by the toolchain, not prose. The TypeScript sibling of CI0001–CI0015:

- **tsconfig:** `strict`, `noUncheckedIndexedAccess`, `noImplicitOverride`.
- **ESLint flat config + local custom plugin** (lives inside the template, unpublished): no comments, function-length cap, parameter-count cap, file line cap, no `any`, no `eslint-disable` directives, one exported component per file.
- **dependency-cruiser:** `features/*` cannot import each other, `lib` cannot import `features` or `app`, no circular dependencies.
- **Jest:** coverage gate (80% lines/branches on `lib` and `features`), plus a check that every exported function in `lib`/`features` has a corresponding test file.
- **Hooks (ported from dotnet):** `pre-commit` (`tsc --noEmit`, eslint, jest), `reference-transaction` (branch must match `feat/<issue#>-<slug>` and issue must exist), `block-main-branch` and `block-merged-branch` Claude Code PreToolUse hooks.

## 4. Submission layer — CLAUDE.md as orchestrator

CLAUDE.md is the routing brain. It documents two lifecycles:

1. **Dev lifecycle** — the same issue → branch → commit → PR flow as the dotnet templates.
2. **Submission lifecycle** — on any submission-related request, the agent must read `SUBMISSION.md` first, determine the current stage, and invoke the matching skill. Gates are never skipped.

`SUBMISSION.md` is the persistent state file: ordered stages with checkboxes and recorded values (bundle ID, ASC app ID, subscription product IDs, RevenueCat API key location). Stage gates encode Apple's ordering rules — e.g. "the first subscription must be attached to a new app version before review." A submission interrupted on Tuesday resumes cleanly on Thursday.

Stage 0 is a human-prerequisites checklist, agent-verified: Apple Developer Program membership, ASC API `.p8` key, RevenueCat account, demo account for App Review.

Skills in `.claude/skills/`:

| Skill | Drives | Does |
|---|---|---|
| `submission-doctor` | local + read-only checks | Audits readiness (icons, privacy strings, bundle ID, IAP wiring), reports the next stage |
| `asc-setup` | Playwright | App record, subscription group + products, metadata, review info, demo account |
| `revenuecat-setup` | Playwright | Project, entitlements, offerings; writes API keys back into `.env` |
| `build-and-submit` | EAS CLI | `eas build` → `eas submit`, attach IAP products to the version |

## 5. Error handling

- Playwright skills are idempotent: snapshot the current ASC/RevenueCat state before mutating, skip steps already complete. Re-running any stage is always safe.
- On selector breakage or unexpected page state, a skill stops and reports rather than guessing.
- EAS CLI failures surface build logs to the agent; `submission-doctor` validates locally before each gate to fail early.

## 6. Testing

- `expo/template-tests/scaffold-and-validate.ps1`: scaffold → `npm ci` → `tsc --noEmit` → eslint (including a seeded violation proving custom rules fire) → jest → dependency-cruiser.
- CI runs the expo validation job alongside the existing dotnet jobs on every push.
- Submission skills cannot be CI-tested against real ASC/RevenueCat: `submission-doctor`'s local checks are unit-testable; the Playwright skills get a documented manual smoke procedure.

## Out of scope (v1)

- Google Play Console skills (follow-up)
- npm-published `create-*` scaffolder
- Automating ASC steps with no browser/API surface (agreements, tax forms) beyond checklist verification
