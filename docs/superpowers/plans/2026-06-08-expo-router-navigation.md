# Expo Router Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate the Expo template from hand-wired `App.tsx` screen switching to expo-router file-based navigation, with `src/app/` as routes and screens moved to `src/features/` (gated, tested), preserving config-gated auth and all guardrails.

**Architecture:** `src/app/` becomes thin expo-router route files (untested glue). Screens move to `src/features/<name>/` as pure, props-driven components (coverage-gated). A new `AuthProvider` context in `src/lib/auth/` lifts `useAuth` once, runs the RevenueCat identity sync, and feeds a route-group redirect guard — replacing the `AuthGate` component.

**Tech Stack:** expo-router, react-native-safe-area-context, react-native-screens, jest + @testing-library/react-native.

**Spec:** `docs/superpowers/specs/2026-06-08-expo-router-navigation-design.md`

**Working notes for the implementer:**
- Work in `expo/templates/app/`. Harness repo has no pre-commit hooks; commit to `main`. NO COMMENTS / no `any` / no `eslint-disable` in any source/test.
- **expo-router API reconciliation:** the route-file code below targets expo-router for SDK 55. After writing route files, `npx tsc --noEmit` is the gate — if `Stack`, `Stack.Screen`, `Redirect`, or `useRouter` signatures differ from the installed version, adjust to the installed types (read `node_modules/expo-router/*.d.ts`), keep behavior identical, never use `any`. This is expected, not failure.
- **one-component-per-file gotcha:** the custom rule flags any exported uppercase name whose initializer is a function OR a `CallExpression` (to catch `memo()`). So do NOT export a `createContext()` result with an uppercase name — keep the context module-private (only export `AuthProvider` + lowercase `useAuthContext`).
- Tasks 1–3 each leave `npm run verify` green (old `App.tsx` path stays intact while new pieces are added). Task 4 is the atomic switch. Task 5 is docs + CI.

---

## File structure (end state)

```
expo/templates/app/
  package.json                              main -> "expo-router/entry"
  app.config.js                             + "expo-router" plugin
  .dependency-cruiser.cjs                   + features-no-route-import rule
  src/app/                                  ROUTES (glue, not gated)
    _layout.tsx                             initPurchases + <AuthProvider> + root <Stack>
    sign-in.tsx                             -> <SignInScreen auth={useAuthContext()} />
    (app)/_layout.tsx                       loading/guard redirect + group <Stack>
    (app)/index.tsx                         -> <HomeScreen ... onUpgradePress=push('/paywall') />
    (app)/paywall.tsx                       modal -> <PaywallScreen onClose=back() />
  src/lib/auth/
    AuthProvider.tsx (+ __tests__)          NEW: context, single useAuth, purchases sync
  src/features/
    home/HomeScreen.tsx (+ __tests__)       moved from src/app
    auth/SignInScreen.tsx (+ __tests__)     moved from src/app (new test)
    paywall/PaywallScreen.tsx (+ __tests__) moved from src/app (test migrated)
  DELETED: App.tsx, index.ts,
           src/app/{HomeScreen,PaywallScreen,SignInScreen,AuthGate}.tsx,
           src/app/__tests__/{AuthGate,PaywallScreen}.test.tsx
```

---

### Task 1: Install expo-router deps + plugin (non-breaking)

**Files:** `package.json`, `app.config.js`

- [ ] **Step 1: Install**

```powershell
cd expo\templates\app
npx expo install expo-router react-native-safe-area-context react-native-screens
```
Expected: three deps added at expo-managed versions. Keep what it writes.

- [ ] **Step 2: Add the plugin to `app.config.js`**

Find the `plugins` assignment:
```js
const plugins = isProduction
  ? ['expo-apple-authentication']
  : ['expo-dev-client', 'expo-apple-authentication'];
```
Replace with:
```js
const plugins = isProduction
  ? ['expo-router', 'expo-apple-authentication']
  : ['expo-router', 'expo-dev-client', 'expo-apple-authentication'];
```

- [ ] **Step 3: Verify still green**

Run: `npm run verify`
Expected: all gates pass (nothing consumes expo-router yet; `App.tsx` still active). Commit:
```powershell
git add expo
git commit -m "Install expo-router dependencies and plugin"
```

---

### Task 2: AuthProvider context (TDD)

**Files:** Create `src/lib/auth/AuthProvider.tsx`, `src/lib/auth/__tests__/AuthProvider.test.tsx`

- [ ] **Step 1: Write the failing test**

`src/lib/auth/__tests__/AuthProvider.test.tsx`:
```tsx
import { render, screen } from '@testing-library/react-native';
import { Text } from 'react-native';

import { AuthProvider, useAuthContext } from '../AuthProvider';
import * as linkPurchases from '../linkPurchases';
import * as useAuthModule from '../useAuth';
import type { AuthState } from '../useAuth';

jest.mock('../useAuth');
jest.mock('../linkPurchases');

const mockedUseAuth = useAuthModule as jest.Mocked<typeof useAuthModule>;
const mockedLink = linkPurchases as jest.Mocked<typeof linkPurchases>;

function authState(overrides: Partial<AuthState>): AuthState {
  return {
    isAuthEnabled: false,
    isAuthenticated: false,
    isLoading: false,
    user: null,
    signIn: jest.fn(),
    signInWithApple: jest.fn(),
    signOut: jest.fn(),
    getToken: jest.fn(),
    ...overrides
  };
}

function Probe() {
  const auth = useAuthContext();
  return <Text>{auth.isAuthEnabled ? 'on' : 'off'}</Text>;
}

describe('AuthProvider', () => {
  beforeEach(() => jest.clearAllMocks());

  test('provides auth state to consumers', () => {
    mockedUseAuth.useAuth.mockReturnValue(authState({ isAuthEnabled: true }));
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>
    );
    expect(screen.getByText('on')).toBeTruthy();
  });

  test('syncs purchases identity with the current user', () => {
    const user = { id: 'auth0|1', email: null, name: null };
    mockedUseAuth.useAuth.mockReturnValue(authState({ user }));
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>
    );
    expect(mockedLink.syncPurchasesIdentity).toHaveBeenCalledWith(user);
  });

  test('useAuthContext throws outside a provider', () => {
    mockedUseAuth.useAuth.mockReturnValue(authState({}));
    expect(() => render(<Probe />)).toThrow('useAuthContext must be used within an AuthProvider');
  });
});
```

- [ ] **Step 2: Run, verify FAIL**

Run: `npx jest src/lib/auth/__tests__/AuthProvider`
Expected: FAIL — cannot find `../AuthProvider`.

- [ ] **Step 3: Implement `src/lib/auth/AuthProvider.tsx`**

```tsx
import { createContext, ReactNode, useContext, useEffect } from 'react';

import { syncPurchasesIdentity } from './linkPurchases';
import { AuthState, useAuth } from './useAuth';

const AuthContext = createContext<AuthState | null>(null);

type AuthProviderProps = {
  children: ReactNode;
};

export function AuthProvider({ children }: AuthProviderProps) {
  const auth = useAuth();

  useEffect(() => {
    syncPurchasesIdentity(auth.user);
  }, [auth.user]);

  return <AuthContext.Provider value={auth}>{children}</AuthContext.Provider>;
}

export function useAuthContext(): AuthState {
  const value = useContext(AuthContext);
  if (value === null) {
    throw new Error('useAuthContext must be used within an AuthProvider');
  }
  return value;
}
```

- [ ] **Step 4: Run, verify PASS + coverage**

Run: `npx jest src/lib/auth/__tests__/AuthProvider --coverage --collectCoverageFrom="src/lib/auth/AuthProvider.tsx"`
Expected: 3 tests pass; AuthProvider.tsx ≥80% lines/branches (the three tests cover provide, effect, and the throw branch).

- [ ] **Step 5: Full verify + commit**

Run: `npm run verify` → green (the third "throws outside provider" test renders `Probe` without a provider, which logs a React error boundary warning but the test passes; that is expected).
```powershell
git add expo
git commit -m "Add AuthProvider context lifting useAuth and purchases sync"
```

---

### Task 3: Move screens to src/features (TDD, non-breaking)

Old screens in `src/app` stay (App.tsx still imports them) until Task 4. New copies live in `src/features` with corrected import depth and tests.

**Files:** Create `src/features/home/HomeScreen.tsx` (+test), `src/features/auth/SignInScreen.tsx` (+test), `src/features/paywall/PaywallScreen.tsx` (+test).

- [ ] **Step 1: Create `src/features/home/HomeScreen.tsx`** (identical content; no imports to fix)

```tsx
import { Pressable, StyleSheet, Text, View } from 'react-native';

type HomeScreenProps = {
  isSubscribed: boolean;
  onUpgradePress: () => void;
};

export function HomeScreen({ isSubscribed, onUpgradePress }: HomeScreenProps) {
  return (
    <View style={styles.container}>
      <Text style={styles.title}>AppTemplate</Text>
      <Text style={styles.subtitle}>{isSubscribed ? 'Premium active' : 'Free plan'}</Text>
      {!isSubscribed && (
        <Pressable onPress={onUpgradePress} accessibilityRole="button" style={styles.upgrade}>
          <Text style={styles.upgradeText}>Upgrade</Text>
        </Pressable>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#10131a' },
  title: { color: '#ffffff', fontSize: 32, fontWeight: '700' },
  subtitle: { color: '#8a93a6', fontSize: 16, marginTop: 8 },
  upgrade: { marginTop: 24, paddingHorizontal: 24, paddingVertical: 12, borderRadius: 12, backgroundColor: '#4c6ef5' },
  upgradeText: { color: '#ffffff', fontSize: 16, fontWeight: '600' }
});
```

- [ ] **Step 2: Create `src/features/home/__tests__/HomeScreen.test.tsx`**

```tsx
import { fireEvent, render, screen } from '@testing-library/react-native';

import { HomeScreen } from '../HomeScreen';

describe('HomeScreen', () => {
  test('shows the upgrade CTA and free plan when not subscribed', () => {
    render(<HomeScreen isSubscribed={false} onUpgradePress={jest.fn()} />);
    expect(screen.getByText('Upgrade')).toBeTruthy();
    expect(screen.getByText('Free plan')).toBeTruthy();
  });

  test('hides the CTA and shows premium when subscribed', () => {
    render(<HomeScreen isSubscribed={true} onUpgradePress={jest.fn()} />);
    expect(screen.queryByText('Upgrade')).toBeNull();
    expect(screen.getByText('Premium active')).toBeTruthy();
  });

  test('calls onUpgradePress when the CTA is pressed', () => {
    const onUpgradePress = jest.fn();
    render(<HomeScreen isSubscribed={false} onUpgradePress={onUpgradePress} />);
    fireEvent.press(screen.getByText('Upgrade'));
    expect(onUpgradePress).toHaveBeenCalled();
  });
});
```

- [ ] **Step 3: Create `src/features/paywall/PaywallScreen.tsx`** (import depth fixed: `../components` → `../../components`)

```tsx
import { useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';
import Purchases, { PurchasesPackage } from 'react-native-purchases';

import { PackageRow } from '../../components/PackageRow';

type PaywallScreenProps = {
  onClose: () => void;
};

export function PaywallScreen({ onClose }: PaywallScreenProps) {
  const [packages, setPackages] = useState<PurchasesPackage[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    Purchases.getOfferings()
      .then((offerings) => {
        setPackages(offerings.current?.availablePackages ?? []);
        setIsLoading(false);
      })
      .catch(() => setIsLoading(false));
  }, []);

  if (isLoading) {
    return (
      <View style={styles.container}>
        <ActivityIndicator />
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Go Premium</Text>
      {packages.map((pkg) => (
        <PackageRow key={pkg.identifier} pkg={pkg} onPurchased={onClose} />
      ))}
      <Pressable onPress={onClose} accessibilityRole="button">
        <Text style={styles.close}>Not now</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, justifyContent: 'center', padding: 24, backgroundColor: '#10131a' },
  title: { color: '#ffffff', fontSize: 28, fontWeight: '700', marginBottom: 16 },
  close: { color: '#8a93a6', fontSize: 16, textAlign: 'center', marginTop: 24 }
});
```

- [ ] **Step 4: Create `src/features/paywall/__tests__/PaywallScreen.test.tsx`** (migrated; import `../PaywallScreen` unchanged)

```tsx
import { render, screen, waitFor } from '@testing-library/react-native';
import Purchases from 'react-native-purchases';

import { PaywallScreen } from '../PaywallScreen';

const mockedPurchases = Purchases as jest.Mocked<typeof Purchases>;

type Offerings = Awaited<ReturnType<typeof Purchases.getOfferings>>;

function offeringsWith(packages: Array<{ identifier: string; title: string; priceString: string }>): Offerings {
  return {
    current: {
      availablePackages: packages.map((pkg) => ({
        identifier: pkg.identifier,
        product: { title: pkg.title, priceString: pkg.priceString }
      }))
    }
  } as unknown as Offerings;
}

describe('PaywallScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  test('renders packages from the current offering', async () => {
    mockedPurchases.getOfferings.mockResolvedValue(
      offeringsWith([{ identifier: 'monthly', title: 'Plus Monthly', priceString: '£4.99' }])
    );
    render(<PaywallScreen onClose={jest.fn()} />);
    await waitFor(() => expect(screen.getByText('Plus Monthly')).toBeTruthy());
    expect(screen.getByText('£4.99')).toBeTruthy();
  });

  test('renders the close action when offerings fail to load', async () => {
    mockedPurchases.getOfferings.mockRejectedValue(new Error('network'));
    render(<PaywallScreen onClose={jest.fn()} />);
    await waitFor(() => expect(screen.getByText('Not now')).toBeTruthy());
  });
});
```

- [ ] **Step 5: Create `src/features/auth/SignInScreen.tsx`** (import depth fixed: `../lib/auth/useAuth` → `../../lib/auth/useAuth`)

```tsx
import * as AppleAuthentication from 'expo-apple-authentication';
import { ActivityIndicator, Platform, Pressable, StyleSheet, Text, View } from 'react-native';

import type { AuthState } from '../../lib/auth/useAuth';

type SignInScreenProps = {
  auth: AuthState;
};

export function SignInScreen({ auth }: SignInScreenProps) {
  if (auth.isLoading) {
    return (
      <View style={styles.container}>
        <ActivityIndicator />
      </View>
    );
  }
  return (
    <View style={styles.container}>
      <Text style={styles.title}>Sign in</Text>
      <Pressable onPress={auth.signIn} accessibilityRole="button" style={styles.primary}>
        <Text style={styles.primaryText}>Continue</Text>
      </Pressable>
      {Platform.OS === 'ios' && (
        <AppleAuthentication.AppleAuthenticationButton
          buttonType={AppleAuthentication.AppleAuthenticationButtonType.SIGN_IN}
          buttonStyle={AppleAuthentication.AppleAuthenticationButtonStyle.BLACK}
          cornerRadius={12}
          style={styles.apple}
          onPress={auth.signInWithApple}
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: 24, backgroundColor: '#10131a' },
  title: { color: '#ffffff', fontSize: 28, fontWeight: '700', marginBottom: 24 },
  primary: { paddingHorizontal: 32, paddingVertical: 14, borderRadius: 12, backgroundColor: '#4c6ef5' },
  primaryText: { color: '#ffffff', fontSize: 16, fontWeight: '600' },
  apple: { width: 240, height: 48, marginTop: 16 }
});
```

- [ ] **Step 6: Create `src/features/auth/__tests__/SignInScreen.test.tsx`**

```tsx
import { fireEvent, render, screen } from '@testing-library/react-native';

import { SignInScreen } from '../SignInScreen';
import type { AuthState } from '../../../lib/auth/useAuth';

function authState(overrides: Partial<AuthState>): AuthState {
  return {
    isAuthEnabled: true,
    isAuthenticated: false,
    isLoading: false,
    user: null,
    signIn: jest.fn(),
    signInWithApple: jest.fn(),
    signOut: jest.fn(),
    getToken: jest.fn(),
    ...overrides
  };
}

describe('SignInScreen', () => {
  test('shows a spinner and no Continue while loading', () => {
    render(<SignInScreen auth={authState({ isLoading: true })} />);
    expect(screen.queryByText('Continue')).toBeNull();
  });

  test('triggers signIn when Continue is pressed', () => {
    const signIn = jest.fn();
    render(<SignInScreen auth={authState({ isLoading: false, signIn })} />);
    fireEvent.press(screen.getByText('Continue'));
    expect(signIn).toHaveBeenCalled();
  });
});
```

- [ ] **Step 7: Verify + commit**

Run: `npm run verify`
Expected: green. Coverage now collects the three feature screens (all covered); check-test-files passes (each has a test); the old `src/app` screens still satisfy `App.tsx` and are not gated. Commit:
```powershell
git add expo
git commit -m "Move screens to src/features with tests"
```

---

### Task 4: The expo-router switch (atomic)

**Files:** Create `src/app/_layout.tsx`, `src/app/sign-in.tsx`, `src/app/(app)/_layout.tsx`, `src/app/(app)/index.tsx`, `src/app/(app)/paywall.tsx`. Modify `package.json`, `.dependency-cruiser.cjs`. Delete `App.tsx`, `index.ts`, `src/app/{HomeScreen,PaywallScreen,SignInScreen,AuthGate}.tsx`, `src/app/__tests__/{AuthGate,PaywallScreen}.test.tsx`.

- [ ] **Step 1: Create `src/app/_layout.tsx`**

```tsx
import { Stack } from 'expo-router';

import { AuthProvider } from '../lib/auth/AuthProvider';
import { initPurchases } from '../lib/purchases/initPurchases';

initPurchases();

export default function RootLayout() {
  return (
    <AuthProvider>
      <Stack screenOptions={{ headerShown: false }}>
        <Stack.Screen name="(app)" />
        <Stack.Screen name="sign-in" />
      </Stack>
    </AuthProvider>
  );
}
```

- [ ] **Step 2: Create `src/app/sign-in.tsx`**

```tsx
import { SignInScreen } from '../features/auth/SignInScreen';
import { useAuthContext } from '../lib/auth/AuthProvider';

export default function SignIn() {
  return <SignInScreen auth={useAuthContext()} />;
}
```

- [ ] **Step 3: Create `src/app/(app)/_layout.tsx`**

```tsx
import { Redirect, Stack } from 'expo-router';
import { ActivityIndicator, StyleSheet, View } from 'react-native';

import { useAuthContext } from '../../lib/auth/AuthProvider';

export default function AppLayout() {
  const auth = useAuthContext();

  if (auth.isLoading) {
    return (
      <View style={styles.loading}>
        <ActivityIndicator />
      </View>
    );
  }

  if (auth.isAuthEnabled && !auth.isAuthenticated) {
    return <Redirect href="/sign-in" />;
  }

  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="index" />
      <Stack.Screen name="paywall" options={{ presentation: 'modal' }} />
    </Stack>
  );
}

const styles = StyleSheet.create({
  loading: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#10131a' }
});
```

- [ ] **Step 4: Create `src/app/(app)/index.tsx`**

```tsx
import { useRouter } from 'expo-router';

import { HomeScreen } from '../../features/home/HomeScreen';
import { useSubscription } from '../../lib/purchases/useSubscription';

export default function Home() {
  const router = useRouter();
  const { isSubscribed } = useSubscription();
  return <HomeScreen isSubscribed={isSubscribed} onUpgradePress={() => router.push('/paywall')} />;
}
```

- [ ] **Step 5: Create `src/app/(app)/paywall.tsx`**

```tsx
import { useRouter } from 'expo-router';

import { PaywallScreen } from '../../features/paywall/PaywallScreen';

export default function Paywall() {
  const router = useRouter();
  return <PaywallScreen onClose={() => router.back()} />;
}
```

- [ ] **Step 6: Point the entry at expo-router**

In `package.json`, change `"main": "index.ts"` to `"main": "expo-router/entry"`.

- [ ] **Step 7: Add the dependency-cruiser rule**

In `.dependency-cruiser.cjs`, add this object to the `forbidden` array (after the existing `components-no-feature-import` rule):
```js
    {
      name: 'features-no-route-import',
      severity: 'error',
      from: { path: '^src/features/' },
      to: { path: '^src/app/' }
    },
```

- [ ] **Step 8: Delete the old files**

```powershell
Remove-Item App.tsx, index.ts
Remove-Item src\app\HomeScreen.tsx, src\app\PaywallScreen.tsx, src\app\SignInScreen.tsx, src\app\AuthGate.tsx
Remove-Item src\app\__tests__\AuthGate.test.tsx, src\app\__tests__\PaywallScreen.test.tsx
```

- [ ] **Step 9: Typecheck (the reconciliation gate)**

Run: `npx tsc --noEmit`
Expected: clean. If expo-router types differ (e.g. `Redirect` `href` typing, `Stack.Screen` `options`, `useRouter().push` argument), read `node_modules/expo-router/build/*.d.ts` and adjust the call to the installed types — keep behavior identical, no `any`. Common likely deltas: `href` may want a typed route object instead of a bare string when typed-routes are on (they are OFF here, so a string should be accepted); if tsc rejects the string, cast via the documented `Href` type, not `any`.

- [ ] **Step 10: Full verify**

Run: `npm run verify`
Expected: all gates pass. depcruise should report no violations (routes import features/lib; features do not import routes). check-test-files passes (no gated source lost its test — the screens' tests live in features now). coverage holds (lib + features). jest does not execute route files (no test imports them). If lint flags a route file under one-component-per-file, confirm each route file has exactly one default-exported component.

- [ ] **Step 11: Commit**

```powershell
git add -A expo
git commit -m "Switch template to expo-router file-based navigation"
```

---

### Task 5: Docs + validation + CI

**Files:** `CLAUDE.md`, `README.md`, `expo/template-tests/scaffold-and-validate.ps1`

- [ ] **Step 1: Update `CLAUDE.md` Architecture section**

Replace the existing `src/app/` and `src/features/` bullets (the first two architecture bullets) with:
```markdown
- `src/app/` — expo-router routes (file-based). These are thin glue: they own
  router hooks and pass plain props to screens. Not coverage-gated.
- `src/features/<name>/` — screen UIs and feature logic, as pure props-driven
  components. Coverage-gated; never import a sibling feature or `src/app`.
- `src/components/` — shared presentational components. May import `lib` only.
- `src/lib/` — platform/service wrappers (purchases, auth). Never imports
  app/features/components. `AuthProvider` (in `src/lib/auth/`) lifts `useAuth`
  once and the `src/app/(app)/_layout.tsx` guard redirects to `/sign-in` when
  auth is enabled and the user is signed out.
```

- [ ] **Step 2: Update `README.md`**

Replace the "Quick start" `npx expo start` note region — change the line:
```
Note: `react-native-purchases` is a native module — use a development build
```
by inserting BEFORE it:
```markdown
Navigation is file-based (expo-router): routes live in `src/app/`, screens in
`src/features/`.

```

- [ ] **Step 3: Add a scaffold assertion**

In `expo/template-tests/scaffold-and-validate.ps1`, after the "CI workflow must ship" block and before the closing `}` of the `try`, insert:
```powershell
    Write-Host "expo-router root layout must ship in the scaffold..."
    $layoutPath = Join-Path $scaffoldDir 'src\app\_layout.tsx'
    if (-not (Test-Path $layoutPath)) { throw "src/app/_layout.tsx missing from scaffold at $layoutPath" }
```

- [ ] **Step 4: Parse-check the script**

Run from repo root:
```powershell
$null = [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path .\expo\template-tests\scaffold-and-validate.ps1), [ref]$null, [ref]$null); if ($?) { 'parse ok' }
```
Expected: `parse ok`.

- [ ] **Step 5: Commit, push, watch CI**

```powershell
git add expo
git commit -m "Document expo-router navigation and assert it ships"
git push
gh run list --limit 1
```
Then `gh run watch <id> --exit-status`. Report all jobs: `scaffold-and-test (cli)`, `scaffold-and-test (etl-api)`, `expo-scaffold-and-validate` — all must be green. The expo job now scaffolds an app with the full expo-router structure and runs the entire guardrail set against it, which is the real end-to-end proof (it installs deps fresh, so any expo-router resolution issue surfaces here). If it fails, fetch `gh run view <id> --log-failed`, diagnose (expo-router metro/babel resolution, a route file lint issue, or a coverage gap), and fix.

---

## Self-review notes (already applied)

- **Spec coverage:** routes in src/app + screens in src/features gated (T3, T4); AuthProvider/redirect guard replacing AuthGate (T2, T4); thin-route/pure-screen split (T4 route files + T3 screens); deps + plugin (T1); depcruise features-no-route-import (T4); delete App.tsx/index.ts/AuthGate (T4); CLAUDE.md/README (T5); validation assertion (T5). Typed-routes left off per spec.
- **Import-depth consistency:** PaywallScreen `../../components/PackageRow`, SignInScreen `../../lib/auth/useAuth`, their tests `../PaywallScreen` / `../SignInScreen` and (SignInScreen test) `../../../lib/auth/useAuth`. Route files import `../features/...` and `../lib/...` (root) or `../../features/...` and `../../lib/...` (inside `(app)/`). Verified against the directory depths.
- **Type consistency:** `AuthState` (from `src/lib/auth/useAuth`) is the single auth type used by AuthProvider, SignInScreen, sign-in route, and tests. `AuthProvider`/`useAuthContext` names consistent across T2 and T4.
- **Known risk, flagged:** expo-router call signatures may need reconciliation; `npx tsc --noEmit` (T4 Step 9) is the gate, with the documented `Href` cast (not `any`) as the fallback if a bare string href is rejected.
```
