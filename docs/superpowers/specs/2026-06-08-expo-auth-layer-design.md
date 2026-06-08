# Expo Template Auth Layer — Design

**Date:** 2026-06-08
**Status:** Approved

## Goal

Add an authentication layer to the Expo agent-harness template so that enabling
auth (like the existing RevenueCat payments layer) is **just configuration** —
set env vars on the frontend, do the Auth0/Apple provider setup, no per-app
integration code. The auth layer authenticates the user and produces a token;
how that token is consumed server-side is left to each app's implementation.

## Scope decisions

| Decision | Choice |
|---|---|
| Backend | None added. The provider (Auth0) is the backend; the template only authenticates and yields a token. |
| Provider | Auth0 via `expo-auth-session` (OIDC). Matches journal-app. |
| Enablement | Always present, config-gated. Inert when env vars unset (no login wall), exactly like RevenueCat today. |
| Auth ↔ payments | Auto-linked when both on: `Purchases.logIn(userId)` / `logOut()` so entitlements follow the account. Independent when either is off. |
| Apple Sign In | Bundled (`expo-apple-authentication`) so the template is App-Store-compliant out of the box for social login. |
| Platforms | iOS-first (consistent with the rest of the template). Apple button is iOS-only; Auth0 hosted login is cross-platform. |

## 1. Architecture & enablement

A new `src/lib/auth/` layer mirrors `src/lib/purchases/`. It always ships.
Enablement is gated by two env vars:

- `EXPO_PUBLIC_AUTH0_DOMAIN`
- `EXPO_PUBLIC_AUTH0_CLIENT_ID`

When unset, `isAuthEnabled` is `false`: `useAuth()` reports auth-disabled, the
`AuthGate` renders the app directly with no login, and the production env
validation does NOT require the auth vars. Set the vars + configure the Auth0
tenant → login turns on. One code path; no scaffolder flag.

## 2. Components

All auth source lives under `src/lib/auth/` except the two screens/gate under
`src/app/`. Layer rules (dependency-cruiser) still hold: `lib` imports nothing
from `app`/`features`/`components`.

- **`src/lib/auth/authConfig.ts`** — reads the two env vars; exports
  `isAuthEnabled: boolean` and a typed `AuthConfig`. Single source of truth for
  "is auth on."
- **`src/lib/auth/tokenStore.ts`** — thin `expo-secure-store` wrapper
  (`getToken`/`setTokens`/`clear`). Independently testable.
- **`src/lib/auth/useAuth.ts`** — the hook returning:
  `{ isAuthEnabled, isAuthenticated, isLoading, user, signIn, signInWithApple, signOut, getToken }`.
  Wraps `expo-auth-session` for Auth0 hosted login and `expo-apple-authentication`
  for the native Apple path. Persists tokens via `tokenStore`; `getToken()`
  returns a valid access token with silent refresh when expired.
- **`src/lib/auth/linkPurchases.ts`** — the auth↔payments bridge: exports
  `syncPurchasesIdentity(user)` which calls `Purchases.logIn(user.id)` when a
  user is present and `Purchases.logOut()` when null. No-ops harmlessly when
  payments are inert. Auth does NOT import the purchases hook; `App.tsx` wires
  the bridge, keeping the two libs decoupled at source level.
- **`src/app/SignInScreen.tsx`** — login UI: an Auth0 "Continue" button plus the
  native Apple Sign In button (rendered on iOS only). One exported component.
- **`src/app/AuthGate.tsx`** — gating component: `if (isAuthEnabled && !isAuthenticated)`
  renders `SignInScreen`, else renders its children. When auth is off it always
  renders children. Lives in its own file (not inlined in `App.tsx`) so it is
  unit-testable in isolation.

## 3. Data flow

`App.tsx`:
1. `initPurchases()` (existing).
2. `useAuth()` provides auth state.
3. An effect calls `syncPurchasesIdentity(user)` whenever the auth user changes.
4. `<AuthGate>` wraps `<HomeScreen>` / paywall as today.

Apple Sign In feeds its identity token into Auth0's "Sign in with Apple"
connection (token exchange), so there is ONE identity/token model downstream —
both paths resolve to an Auth0 session and user. `getToken()` is the single
accessor an app uses to attach a bearer token to its own backend calls.

## 4. Submission layer changes

- **`.env.example`** — add `EXPO_PUBLIC_AUTH0_DOMAIN=` and
  `EXPO_PUBLIC_AUTH0_CLIENT_ID=`.
- **`app.config.js`** — add `expo-apple-authentication` to plugins; ensure the
  auth redirect scheme is present (the existing `scheme` covers it). Production
  env validation treats the two auth vars as **conditionally required**: enforced
  only if either is set (partial config is an error), so fully-unset (auth-off)
  apps still build.
- **`scripts/submission-doctor.js`** — new checks, active only when auth vars are
  set: (a) both auth vars present (no partial config); (b) `expo-apple-authentication`
  plugin configured (Apple's social-login requirement); (c) redirect scheme set.
  When auth vars are unset, these checks report PASS/skip so auth-off apps remain
  green.
- **`SUBMISSION.md`** — Stage 1 gains an Auth0 setup line (application config,
  callback + logout URLs, enable the Apple connection) and a note that enabling
  any social login requires Apple Sign In.
- **New skill `.claude/skills/auth-setup/SKILL.md`** (Playwright-driven, like
  `revenuecat-setup`) — walks the Auth0 dashboard: create/configure the native
  application, set callback/logout URLs to the app's redirect, enable the Apple
  connection, and write the public values back into `.env`. Idempotent; stops at
  login/2FA and never handles client secrets in the repo.
- **`CLAUDE.md`** — routing table gains `auth-setup` under Stage 1; the
  architecture section documents `src/lib/auth/` and the `getToken()` contract.

## 5. Guardrails & testing

All new code stays under the existing error-severity gates (no comments, layer
rules, one-component-per-file, 80% coverage on `src/lib`). `auth/` is a `lib`
module, so every file needs a colocated `__tests__/<name>.test.ts(x)`.

`jest.setup.js` gains mocks for `expo-auth-session`, `expo-apple-authentication`,
and `expo-secure-store`. Test coverage:

- `authConfig`: enabled when both vars set; disabled when unset; partial config
  reported as disabled (validation handles the error path).
- `tokenStore`: set→get round-trips; clear removes; missing token returns null.
- `useAuth`: inert when unconfigured (no login attempted); sign-in success sets
  user + persists tokens; sign-in failure leaves unauthenticated; sign-out clears
  tokens; `getToken` refreshes an expired token.
- `linkPurchases`: calls `Purchases.logIn` with the user id when present and
  `logOut` when null; no throw when payments inert.
- `AuthGate`: renders children when auth off; renders `SignInScreen` when enabled
  and unauthenticated; renders children when authenticated.

`scaffold-and-validate.ps1` gains an assertion: with the auth env vars set in a
throwaway `.env.production`, `submission-doctor` flags missing Apple Sign In if
the plugin were absent (proves the guardrail fires). Default fresh-scaffold
behavior (auth off) stays green.

## Out of scope (v1)

- Any backend / token-verification server (per-app concern).
- Provider-agnostic adapter interface (Auth0 is the one implementation).
- Android-specific Apple Sign In handling beyond Auth0's web flow.
- Account management UI (profile, delete account) beyond sign-in/sign-out.
