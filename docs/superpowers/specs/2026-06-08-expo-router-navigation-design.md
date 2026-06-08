# Expo Router Navigation — Design

**Date:** 2026-06-08
**Status:** Approved

## Goal

Replace the hand-wired `App.tsx` screen switching in the Expo template with
expo-router (file-based navigation). `src/app/` becomes the routes directory;
the existing screen components move to `src/features/`. The config-gated auth
behavior, the paywall flow, and every existing guardrail are preserved. This is
the navigation foundation that account deletion (a settings screen) and future
screens build on.

## Scope decisions

| Decision | Choice |
|---|---|
| Navigation library | expo-router (file-based, the Expo default). |
| Routes directory | `src/app/` (consistent with the template's `src/` convention). |
| Screen location | `src/features/<name>/` — coverage-gated (≥80%), test-file required. |
| Route/screen split | Route files own router hooks and pass plain props to pure, testable screens. |
| Auth gate | A redirect in the root layout (idiomatic expo-router), preserving config-gated inertness. |
| Typed routes | Off in v1 (avoids generated-file / tsc friction); future toggle. |

## 1. Structure (end state)

```
src/app/                       routes (glue; NOT coverage-gated, like App.tsx was)
  _layout.tsx                  root: initPurchases, useAuth, purchases-identity effect, auth guard, <Stack>
  (app)/_layout.tsx            protected group layout
  (app)/index.tsx              home route -> <HomeScreen>
  sign-in.tsx                  -> <SignInScreen>
  paywall.tsx                  modal route -> <PaywallScreen>
src/features/
  home/HomeScreen.tsx          + __tests__/HomeScreen.test.tsx (new test)
  auth/SignInScreen.tsx        + __tests__/SignInScreen.test.tsx (new test; was untested in src/app)
  paywall/PaywallScreen.tsx    + __tests__/PaywallScreen.test.tsx (migrated)
src/components/PackageRow.tsx  unchanged
src/lib/                       unchanged (purchases, auth)
```

Deleted: `App.tsx`, `index.ts`, `src/app/AuthGate.tsx` (+ its test). `package.json`
`main` becomes `expo-router/entry`.

Layer/import rules after the move:
- `src/app` (routes) may import `features`, `components`, `lib`.
- `src/features/<x>` may import `components`, `lib`; not a sibling feature; not `src/app`.
- `src/components` may import `lib` only.
- `src/lib` imports nothing upward.

## 2. The auth guard (config-gated inertness preserved)

The `AuthGate` component is replaced by redirect logic in `src/app/_layout.tsx`,
which also runs `initPurchases()` and the `syncPurchasesIdentity(auth.user)`
effect (the responsibilities `App.tsx` holds today). Behavior:

- `auth.isLoading` → render a neutral loading view (no flash of either screen).
- `auth.isAuthEnabled && !auth.isAuthenticated` → `<Redirect href="/sign-in" />`.
- otherwise → render the `(app)` stack.

When `isAuthEnabled` is false (no Auth0 env vars), there is never a redirect — the
app renders straight through, identical to today's inert path. The `sign-in` and
`paywall` routes are reachable regardless; the guard only walls the `(app)` group.

## 3. Route/screen split (keeps coverage achievable)

Route files own router hooks; screens receive plain props and stay pure:

- `(app)/index.tsx`: `const router = useRouter();` →
  `<HomeScreen isSubscribed={useSubscription().isSubscribed} onUpgradePress={() => router.push('/paywall')} />`.
- `paywall.tsx`: `<PaywallScreen onClose={() => router.back()} />`.
- `sign-in.tsx`: `<SignInScreen auth={useAuth()} />`.

Because screens take props (no router hooks), they are unit-testable with
`@testing-library/react-native` without mocking the router. The router glue in
`src/app/` stays untested, exactly as `App.tsx` is today.

## 4. Guardrail and config updates

- **Dependencies:** `npx expo install expo-router react-native-safe-area-context
  react-native-screens` (expo-managed versions).
- **app.config.js:** add `expo-router` to the `plugins` array (in both the
  production and non-production branches). `scheme` is already present (required
  by expo-router for deep links).
- **dependency-cruiser (`.dependency-cruiser.cjs`):** keep all existing rules;
  add one forbidding `src/features` from importing `src/app`
  (`features-no-route-import`).
- **jest:** screens now live under `src/features`, so they are coverage-collected
  and require colocated test files automatically (`check-test-files` ROOTS already
  = `src/lib` + `src/features`). `PaywallScreen`'s test migrates with it;
  `HomeScreen` and `SignInScreen` gain new tests; `AuthGate`'s test is removed.
- **CLAUDE.md:** update the Architecture section — `src/app/` = expo-router routes
  (glue), `src/features/<name>/` = screens + feature logic (tested), the auth
  guard lives in `src/app/_layout.tsx`. Update Key files.
- **README:** note file-based routing and the routes-vs-screens split in the
  structure description.

## 5. Testing and validation

- `HomeScreen`, `SignInScreen`, `PaywallScreen` get props-driven tests covering
  their observable behavior (e.g. paywall renders offering packages and the close
  action; sign-in shows Continue + the Apple button on iOS; home shows the upgrade
  CTA only when not subscribed). Auth-enabled and auth-disabled behavior is
  exercised through `SignInScreen`'s `auth` prop and the screen tests.
- `expo/template-tests/scaffold-and-validate.ps1` already runs the full guardrail
  set on a fresh scaffold; add one structural assertion that
  `src/app/_layout.tsx` exists in the scaffold. CI validates the whole restructure
  end-to-end.

## Out of scope (v1)

- Typed routes (`experiments.typedRoutes`).
- Tabs or drawer navigation (a single stack is enough; account deletion adds a
  settings route in its own spec).
- Web-specific routing concerns beyond what expo-router provides by default.
- Testing the router glue in `src/app/` (untested by design, like `App.tsx`).
