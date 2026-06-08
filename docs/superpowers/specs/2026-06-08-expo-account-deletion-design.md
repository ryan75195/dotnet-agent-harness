# Expo Account Deletion — Design

**Date:** 2026-06-08
**Status:** Approved

## Goal

Add an in-app account deletion flow to the Expo template. Apple Guideline
5.1.1(v) requires apps that support account creation to offer in-app account
deletion; the template ships Auth0 login but no deletion, a latent App Store
rejection. This closes it: a settings screen, a two-step confirm, a deletion
call to the developer's backend endpoint, and local session wipe — config-gated
so it is inert when auth is off.

## Scope decisions

| Decision | Choice |
|---|---|
| Deletion mechanism | Client flow + documented endpoint contract. The template ships the complete client; the developer's backend performs the privileged Auth0 Management API delete. |
| Config gating | Deletion is relevant only when auth is enabled AND `EXPO_PUBLIC_ACCOUNT_DELETE_URL` is set. |
| Confirm UX | Two-step in-screen confirm (component state), not a native `Alert` — fully testable without mocking. |
| Doctor enforcement | `submission-doctor` FAILS when auth is enabled but no delete URL is set (turns the Apple requirement into a build-time guardrail). |
| AuthState | Unchanged — deletion composes existing `getToken`/`signOut`, avoiding churn to every auth test helper. |

## 1. Components

- **`src/lib/auth/authConfig.ts`** — add `getAccountDeleteUrl(): string | null`
  reading `EXPO_PUBLIC_ACCOUNT_DELETE_URL` (empty/unset → null).
- **`src/lib/auth/authActions.ts`** — add
  `requestAccountDeletion(deleteUrl: string, accessToken: string): Promise<boolean>`:
  `DELETE <deleteUrl>` with `Authorization: Bearer <accessToken>`; returns
  `response.ok`. Pure, fetch-mockable.
- **`src/lib/auth/useDeleteAccount.ts`** (new, coverage-gated, tested) — hook
  consuming `useAuthContext`. Returns
  `{ canDeleteAccount: boolean, deleteAccount: () => Promise<boolean> }`.
  - `canDeleteAccount` = `getAccountDeleteUrl() !== null && auth.isAuthenticated`.
  - `deleteAccount()`: url = `getAccountDeleteUrl()`; if null → return false.
    token = `await auth.getToken()`; if null → return false.
    ok = `await requestAccountDeletion(url, token)`; if ok → `await auth.signOut()`;
    return ok. A failed server call NEVER signs the user out (no false "deleted").
- **`src/features/settings/SettingsScreen.tsx`** (new, gated, tested) — pure
  props-driven screen. Props:
  `{ isAuthenticated: boolean; canDeleteAccount: boolean; onSignOut: () => void; onDeleteAccount: () => void }`.
  Renders a sign-out button when `isAuthenticated`. Renders a delete section only
  when `canDeleteAccount`: first press reveals a confirm row ("This permanently
  deletes your account") with Confirm (calls `onDeleteAccount`) and Cancel (hides
  the confirm). Confirm state is local component state.
- **`src/app/(app)/settings.tsx`** (route glue) — `useAuthContext` +
  `useDeleteAccount` + `useRouter`. `onSignOut` → `auth.signOut()` then
  `router.replace('/')`. `onDeleteAccount` → `deleteAccount()`; on success
  `router.replace('/')`. The `(app)` layout guard then redirects to `/sign-in`.
- **`src/features/home/HomeScreen.tsx`** — add optional
  `onSettingsPress?: () => void`; render a settings affordance only when it is
  provided.
- **`src/app/(app)/index.tsx`** (route glue) — add `useAuthContext`; pass
  `onSettingsPress={auth.isAuthenticated ? () => router.push('/settings') : undefined}`.

## 2. Config gating and the compliance check

- Auth **off** → `isAuthenticated` is false → no settings affordance on home; the
  settings route, if reached directly, shows nothing account-related. Nothing
  changes for a no-auth app.
- Auth **on**, delete URL **unset** → delete section hidden, and
  `submission-doctor` FAILS: `account deletion endpoint required when auth is
  enabled (Apple 5.1.1(v))`. Check is skipped (PASS) when auth is off.
- Auth **on**, delete URL **set** → full flow.

`.env.example` gains `EXPO_PUBLIC_ACCOUNT_DELETE_URL=`. `SUBMISSION.md` gains a
recorded value and a Stage 1 item. `CLAUDE.md` notes deletion in the auth
architecture bullet.

## 3. Endpoint contract (documented, not shipped)

README gains an "Account deletion endpoint" section specifying the contract the
developer's backend must satisfy:

> `DELETE <EXPO_PUBLIC_ACCOUNT_DELETE_URL>` with header
> `Authorization: Bearer <user access token>`. The server MUST: verify the token
> against the Auth0 JWKS, extract the `sub` claim, call the Auth0 Management API
> to delete that user, and return `204`. The Management API client secret lives
> only on the server, never in the app.

Plus a short Node reference snippet (illustrative, not shippable template code)
showing token verification → Management API delete → 204.

## 4. Testing

- `requestAccountDeletion`: fetch mocked — ok response → true; non-ok → false;
  asserts the `DELETE` method and Bearer header.
- `useDeleteAccount`: mock `useAuthContext` + `authActions` + `authConfig`.
  - success: URL set, token present, server ok → calls `signOut`, returns true.
  - no URL → returns false, `signOut` NOT called.
  - no token → returns false, `signOut` NOT called.
  - server failure → returns false, `signOut` NOT called (no false deletion).
  - `canDeleteAccount` true only when URL set and authenticated.
- `SettingsScreen`: sign-out button calls `onSignOut`; delete section hidden when
  `canDeleteAccount` false; first delete press reveals confirm; Cancel hides it;
  Confirm calls `onDeleteAccount`.
- `HomeScreen`: settings affordance shown when `onSettingsPress` provided, absent
  otherwise.
- All gated screens/lib stay ≥80% lines/branches.

## 5. Validation

`expo/template-tests/scaffold-and-validate.ps1` already runs the full guardrail
set on a fresh scaffold. Add one assertion: with auth env vars set but no delete
URL, `submission-doctor` reports the deletion-endpoint FAIL (proving the
guardrail fires). The fresh-scaffold default (auth off) stays green.

## Out of scope (v1)

- A real server-side deletion endpoint (documented contract only).
- A full settings screen beyond sign-out + delete (kept minimal/extensible).
- Data-export / GDPR flows beyond deletion.
- Re-authentication before deletion (the bearer token is the proof of identity;
  the backend enforces token freshness).
