# Expo Account Deletion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a config-gated in-app account deletion flow to the Expo template (Apple 5.1.1(v)): settings screen, two-step confirm, a `DELETE` to the developer's backend endpoint, local session wipe, and a doctor check that fails when auth is on but no delete endpoint is configured.

**Architecture:** Pure lib functions (`getAccountDeleteUrl`, `requestAccountDeletion`) under a `useDeleteAccount` hook that composes the existing `getToken`/`signOut` — no change to `AuthState`. A pure `SettingsScreen` in `src/features`, a thin settings route, and a home settings affordance. The server-side delete is documented contract only.

**Tech Stack:** expo-router, jest + @testing-library/react-native. Frontend-only (the privileged Auth0 Management API delete lives on the developer's backend).

**Spec:** `docs/superpowers/specs/2026-06-08-expo-account-deletion-design.md`

**Working notes:**
- Work in `expo/templates/app/`. No pre-commit hooks; commit to `main`. NO COMMENTS / no `any` / no `eslint-disable`.
- Gated dirs (`src/lib`, `src/features`) need colocated tests + ≥80% coverage. Route files in `src/app/` are untested glue.
- The env var is `EXPO_PUBLIC_ACCOUNT_DELETE_URL` everywhere (authConfig, doctor, .env.example, SUBMISSION.md, CLAUDE.md, README) — keep it identical.

---

## File structure (additions/changes)

```
src/lib/auth/
  authConfig.ts                 + getAccountDeleteUrl()
  authActions.ts                + requestAccountDeletion()
  useDeleteAccount.ts (+test)   NEW hook
src/features/
  settings/SettingsScreen.tsx (+test)   NEW pure screen
  home/HomeScreen.tsx           + optional onSettingsPress (test updated)
src/app/(app)/
  settings.tsx                  NEW route glue
  _layout.tsx                   + <Stack.Screen name="settings" />
  index.tsx                     + onSettingsPress wiring (useAuthContext)
scripts/submission-doctor.js    + deletion-endpoint check
.env.example, SUBMISSION.md, CLAUDE.md, README.md   docs/config
expo/template-tests/scaffold-and-validate.ps1       + assertion
```

---

### Task 1: Lib primitives — getAccountDeleteUrl + requestAccountDeletion (TDD)

**Files:** Modify `src/lib/auth/authConfig.ts`, `src/lib/auth/__tests__/authConfig.test.ts`, `src/lib/auth/authActions.ts`, `src/lib/auth/__tests__/authActions.test.ts`

- [ ] **Step 1: Add failing tests**

Append to `src/lib/auth/__tests__/authConfig.test.ts` a new describe (and add `getAccountDeleteUrl` to the existing import from `../authConfig`):
```ts
describe('getAccountDeleteUrl', () => {
  beforeEach(() => {
    delete process.env.EXPO_PUBLIC_ACCOUNT_DELETE_URL;
  });

  test('returns null when unset', () => {
    expect(getAccountDeleteUrl()).toBeNull();
  });

  test('returns the url when set', () => {
    process.env.EXPO_PUBLIC_ACCOUNT_DELETE_URL = 'https://api.example.com/account';
    expect(getAccountDeleteUrl()).toBe('https://api.example.com/account');
  });
});
```

Append to `src/lib/auth/__tests__/authActions.test.ts` (add `requestAccountDeletion` to the import from `../authActions`):
```ts
describe('requestAccountDeletion', () => {
  test('sends a DELETE with the bearer token and returns true on ok', async () => {
    global.fetch = jest.fn().mockResolvedValue({ ok: true }) as unknown as typeof fetch;
    const result = await requestAccountDeletion('https://api.example.com/account', 'tok-1');
    expect(result).toBe(true);
    expect(global.fetch).toHaveBeenCalledWith('https://api.example.com/account', {
      method: 'DELETE',
      headers: { Authorization: 'Bearer tok-1' }
    });
  });

  test('returns false when the server responds not-ok', async () => {
    global.fetch = jest.fn().mockResolvedValue({ ok: false }) as unknown as typeof fetch;
    expect(await requestAccountDeletion('https://api.example.com/account', 'tok-1')).toBe(false);
  });
});
```

- [ ] **Step 2: Run, verify FAIL**

Run: `npx jest src/lib/auth/__tests__/authConfig src/lib/auth/__tests__/authActions`
Expected: FAIL — `getAccountDeleteUrl`/`requestAccountDeletion` are not exported.

- [ ] **Step 3: Implement**

Append to `src/lib/auth/authConfig.ts`:
```ts
export function getAccountDeleteUrl(): string | null {
  const url = process.env.EXPO_PUBLIC_ACCOUNT_DELETE_URL ?? '';
  return url === '' ? null : url;
}
```

Append to `src/lib/auth/authActions.ts`:
```ts
export async function requestAccountDeletion(deleteUrl: string, accessToken: string): Promise<boolean> {
  const response = await fetch(deleteUrl, {
    method: 'DELETE',
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return response.ok;
}
```

- [ ] **Step 4: Run, verify PASS + commit**

Run: `npx jest src/lib/auth/__tests__/authConfig src/lib/auth/__tests__/authActions` → PASS.
Run: `npm run verify` → green (coverage on the two files stays 100%).
```powershell
git add expo
git commit -m "Add account-delete URL config and deletion request"
```

---

### Task 2: useDeleteAccount hook (TDD)

**Files:** Create `src/lib/auth/useDeleteAccount.ts`, `src/lib/auth/__tests__/useDeleteAccount.test.tsx`

- [ ] **Step 1: Write the failing test**

`src/lib/auth/__tests__/useDeleteAccount.test.tsx`:
```tsx
import { renderHook } from '@testing-library/react-native';

import * as AuthProvider from '../AuthProvider';
import * as authActions from '../authActions';
import * as authConfig from '../authConfig';
import { useDeleteAccount } from '../useDeleteAccount';
import type { AuthState } from '../useAuth';

jest.mock('../AuthProvider');
jest.mock('../authActions');
jest.mock('../authConfig');

const mockedContext = AuthProvider as jest.Mocked<typeof AuthProvider>;
const mockedActions = authActions as jest.Mocked<typeof authActions>;
const mockedConfig = authConfig as jest.Mocked<typeof authConfig>;

function authState(overrides: Partial<AuthState>): AuthState {
  return {
    isAuthEnabled: true,
    isAuthenticated: true,
    isLoading: false,
    user: null,
    signIn: jest.fn(),
    signInWithApple: jest.fn(),
    signOut: jest.fn(),
    getToken: jest.fn().mockResolvedValue('tok-1'),
    ...overrides
  };
}

describe('useDeleteAccount', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockedConfig.getAccountDeleteUrl.mockReturnValue('https://api.example.com/account');
  });

  test('canDeleteAccount is true when url set and authenticated', () => {
    mockedContext.useAuthContext.mockReturnValue(authState({ isAuthenticated: true }));
    const { result } = renderHook(() => useDeleteAccount());
    expect(result.current.canDeleteAccount).toBe(true);
  });

  test('canDeleteAccount is false when the url is unset', () => {
    mockedConfig.getAccountDeleteUrl.mockReturnValue(null);
    mockedContext.useAuthContext.mockReturnValue(authState({ isAuthenticated: true }));
    const { result } = renderHook(() => useDeleteAccount());
    expect(result.current.canDeleteAccount).toBe(false);
  });

  test('deletes and signs out on success', async () => {
    const signOut = jest.fn();
    mockedContext.useAuthContext.mockReturnValue(authState({ signOut }));
    mockedActions.requestAccountDeletion.mockResolvedValue(true);
    const { result } = renderHook(() => useDeleteAccount());
    const ok = await result.current.deleteAccount();
    expect(ok).toBe(true);
    expect(mockedActions.requestAccountDeletion).toHaveBeenCalledWith('https://api.example.com/account', 'tok-1');
    expect(signOut).toHaveBeenCalled();
  });

  test('returns false and does not sign out when the url is unset', async () => {
    const signOut = jest.fn();
    mockedConfig.getAccountDeleteUrl.mockReturnValue(null);
    mockedContext.useAuthContext.mockReturnValue(authState({ signOut }));
    const { result } = renderHook(() => useDeleteAccount());
    expect(await result.current.deleteAccount()).toBe(false);
    expect(signOut).not.toHaveBeenCalled();
  });

  test('returns false and does not sign out when there is no token', async () => {
    const signOut = jest.fn();
    mockedContext.useAuthContext.mockReturnValue(authState({ signOut, getToken: jest.fn().mockResolvedValue(null) }));
    const { result } = renderHook(() => useDeleteAccount());
    expect(await result.current.deleteAccount()).toBe(false);
    expect(signOut).not.toHaveBeenCalled();
  });

  test('returns false and does not sign out when the server delete fails', async () => {
    const signOut = jest.fn();
    mockedContext.useAuthContext.mockReturnValue(authState({ signOut }));
    mockedActions.requestAccountDeletion.mockResolvedValue(false);
    const { result } = renderHook(() => useDeleteAccount());
    expect(await result.current.deleteAccount()).toBe(false);
    expect(signOut).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run, verify FAIL**

Run: `npx jest src/lib/auth/__tests__/useDeleteAccount`
Expected: FAIL — cannot find `../useDeleteAccount`.

- [ ] **Step 3: Implement `src/lib/auth/useDeleteAccount.ts`**

```ts
import { useCallback } from 'react';

import { useAuthContext } from './AuthProvider';
import { requestAccountDeletion } from './authActions';
import { getAccountDeleteUrl } from './authConfig';

export type DeleteAccountState = {
  canDeleteAccount: boolean;
  deleteAccount: () => Promise<boolean>;
};

export function useDeleteAccount(): DeleteAccountState {
  const auth = useAuthContext();
  const deleteUrl = getAccountDeleteUrl();

  const deleteAccount = useCallback(async () => {
    if (deleteUrl === null) {
      return false;
    }
    const token = await auth.getToken();
    if (token === null) {
      return false;
    }
    const ok = await requestAccountDeletion(deleteUrl, token);
    if (ok) {
      await auth.signOut();
    }
    return ok;
  }, [auth, deleteUrl]);

  return {
    canDeleteAccount: deleteUrl !== null && auth.isAuthenticated,
    deleteAccount
  };
}
```

- [ ] **Step 4: Run, verify PASS + coverage + commit**

Run: `npx jest src/lib/auth/__tests__/useDeleteAccount --coverage --collectCoverageFrom="src/lib/auth/useDeleteAccount.ts"` → 6 pass, ≥80% (all branches: url-null, token-null, ok true/false, canDeleteAccount).
Run: `npm run verify` → green.
```powershell
git add expo
git commit -m "Add useDeleteAccount hook composing token, delete, and sign-out"
```

---

### Task 3: SettingsScreen + routes + home affordance

**Files:** Create `src/features/settings/SettingsScreen.tsx` (+test), `src/app/(app)/settings.tsx`. Modify `src/features/home/HomeScreen.tsx` (+test), `src/app/(app)/_layout.tsx`, `src/app/(app)/index.tsx`.

- [ ] **Step 1: Write the failing SettingsScreen test**

`src/features/settings/__tests__/SettingsScreen.test.tsx`:
```tsx
import { fireEvent, render, screen } from '@testing-library/react-native';

import { SettingsScreen } from '../SettingsScreen';

function props(overrides: Partial<Parameters<typeof SettingsScreen>[0]> = {}) {
  return {
    isAuthenticated: true,
    canDeleteAccount: true,
    onSignOut: jest.fn(),
    onDeleteAccount: jest.fn(),
    ...overrides
  };
}

describe('SettingsScreen', () => {
  test('sign out calls onSignOut', () => {
    const onSignOut = jest.fn();
    render(<SettingsScreen {...props({ onSignOut })} />);
    fireEvent.press(screen.getByText('Sign out'));
    expect(onSignOut).toHaveBeenCalled();
  });

  test('hides sign out when not authenticated', () => {
    render(<SettingsScreen {...props({ isAuthenticated: false })} />);
    expect(screen.queryByText('Sign out')).toBeNull();
  });

  test('hides the delete section when canDeleteAccount is false', () => {
    render(<SettingsScreen {...props({ canDeleteAccount: false })} />);
    expect(screen.queryByText('Delete account')).toBeNull();
  });

  test('confirm flow: reveal, cancel, and confirm', () => {
    const onDeleteAccount = jest.fn();
    render(<SettingsScreen {...props({ onDeleteAccount })} />);
    fireEvent.press(screen.getByText('Delete account'));
    expect(screen.getByText('This permanently deletes your account.')).toBeTruthy();
    fireEvent.press(screen.getByText('Cancel'));
    expect(screen.queryByText('This permanently deletes your account.')).toBeNull();
    fireEvent.press(screen.getByText('Delete account'));
    fireEvent.press(screen.getByText('Confirm delete'));
    expect(onDeleteAccount).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run, verify FAIL**

Run: `npx jest src/features/settings`
Expected: FAIL — cannot find `../SettingsScreen`.

- [ ] **Step 3: Implement `src/features/settings/SettingsScreen.tsx`**

```tsx
import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

type SettingsScreenProps = {
  isAuthenticated: boolean;
  canDeleteAccount: boolean;
  onSignOut: () => void;
  onDeleteAccount: () => void;
};

export function SettingsScreen({ isAuthenticated, canDeleteAccount, onSignOut, onDeleteAccount }: SettingsScreenProps) {
  const [confirming, setConfirming] = useState(false);

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Settings</Text>
      {isAuthenticated && (
        <Pressable onPress={onSignOut} accessibilityRole="button" style={styles.row}>
          <Text style={styles.rowText}>Sign out</Text>
        </Pressable>
      )}
      {canDeleteAccount && !confirming && (
        <Pressable onPress={() => setConfirming(true)} accessibilityRole="button" style={styles.row}>
          <Text style={styles.danger}>Delete account</Text>
        </Pressable>
      )}
      {canDeleteAccount && confirming && (
        <View style={styles.confirm}>
          <Text style={styles.confirmText}>This permanently deletes your account.</Text>
          <Pressable onPress={onDeleteAccount} accessibilityRole="button" style={styles.row}>
            <Text style={styles.danger}>Confirm delete</Text>
          </Pressable>
          <Pressable onPress={() => setConfirming(false)} accessibilityRole="button" style={styles.row}>
            <Text style={styles.rowText}>Cancel</Text>
          </Pressable>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, padding: 24, backgroundColor: '#10131a' },
  title: { color: '#ffffff', fontSize: 28, fontWeight: '700', marginBottom: 24 },
  row: { paddingVertical: 14 },
  rowText: { color: '#ffffff', fontSize: 16 },
  danger: { color: '#ff6b6b', fontSize: 16, fontWeight: '600' },
  confirm: { marginTop: 8 },
  confirmText: { color: '#8a93a6', fontSize: 14, marginBottom: 8 }
});
```

- [ ] **Step 4: Run, verify PASS**

Run: `npx jest src/features/settings` → 4 pass.

- [ ] **Step 5: Create the settings route `src/app/(app)/settings.tsx`**

```tsx
import { useRouter } from 'expo-router';

import { SettingsScreen } from '../../features/settings/SettingsScreen';
import { useAuthContext } from '../../lib/auth/AuthProvider';
import { useDeleteAccount } from '../../lib/auth/useDeleteAccount';

export default function Settings() {
  const router = useRouter();
  const auth = useAuthContext();
  const { canDeleteAccount, deleteAccount } = useDeleteAccount();

  const handleSignOut = async () => {
    await auth.signOut();
    router.replace('/');
  };

  const handleDelete = async () => {
    const ok = await deleteAccount();
    if (ok) {
      router.replace('/');
    }
  };

  return (
    <SettingsScreen
      isAuthenticated={auth.isAuthenticated}
      canDeleteAccount={canDeleteAccount}
      onSignOut={handleSignOut}
      onDeleteAccount={handleDelete}
    />
  );
}
```

- [ ] **Step 6: Declare the settings screen in `src/app/(app)/_layout.tsx`**

In the returned `<Stack>`, add a screen line after the `paywall` screen:
```tsx
      <Stack.Screen name="settings" />
```
So the stack reads:
```tsx
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="index" />
      <Stack.Screen name="paywall" options={{ presentation: 'modal' }} />
      <Stack.Screen name="settings" />
    </Stack>
```

- [ ] **Step 7: Add the settings affordance to `HomeScreen` + update its test**

Replace `src/features/home/HomeScreen.tsx` with:
```tsx
import { Pressable, StyleSheet, Text, View } from 'react-native';

type HomeScreenProps = {
  isSubscribed: boolean;
  onUpgradePress: () => void;
  onSettingsPress?: () => void;
};

export function HomeScreen({ isSubscribed, onUpgradePress, onSettingsPress }: HomeScreenProps) {
  return (
    <View style={styles.container}>
      <Text style={styles.title}>AppTemplate</Text>
      <Text style={styles.subtitle}>{isSubscribed ? 'Premium active' : 'Free plan'}</Text>
      {!isSubscribed && (
        <Pressable onPress={onUpgradePress} accessibilityRole="button" style={styles.upgrade}>
          <Text style={styles.upgradeText}>Upgrade</Text>
        </Pressable>
      )}
      {onSettingsPress && (
        <Pressable onPress={onSettingsPress} accessibilityRole="button" style={styles.settings}>
          <Text style={styles.settingsText}>Settings</Text>
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
  upgradeText: { color: '#ffffff', fontSize: 16, fontWeight: '600' },
  settings: { marginTop: 16, paddingHorizontal: 24, paddingVertical: 12 },
  settingsText: { color: '#8a93a6', fontSize: 16 }
});
```

Append two tests to `src/features/home/__tests__/HomeScreen.test.tsx`:
```tsx
  test('shows the settings affordance when onSettingsPress is provided', () => {
    const onSettingsPress = jest.fn();
    render(<HomeScreen isSubscribed={true} onUpgradePress={jest.fn()} onSettingsPress={onSettingsPress} />);
    fireEvent.press(screen.getByText('Settings'));
    expect(onSettingsPress).toHaveBeenCalled();
  });

  test('hides the settings affordance when onSettingsPress is absent', () => {
    render(<HomeScreen isSubscribed={true} onUpgradePress={jest.fn()} />);
    expect(screen.queryByText('Settings')).toBeNull();
  });
```

- [ ] **Step 8: Wire the home route `src/app/(app)/index.tsx`**

Replace with:
```tsx
import { useRouter } from 'expo-router';

import { HomeScreen } from '../../features/home/HomeScreen';
import { useAuthContext } from '../../lib/auth/AuthProvider';
import { useSubscription } from '../../lib/purchases/useSubscription';

export default function Home() {
  const router = useRouter();
  const auth = useAuthContext();
  const { isSubscribed } = useSubscription();
  return (
    <HomeScreen
      isSubscribed={isSubscribed}
      onUpgradePress={() => router.push('/paywall')}
      onSettingsPress={auth.isAuthenticated ? () => router.push('/settings') : undefined}
    />
  );
}
```

- [ ] **Step 9: Full verify + commit**

Run: `npm run verify`
Expected: all gates pass. SettingsScreen + HomeScreen ≥80% coverage; depcruise clean (settings route imports features+lib; SettingsScreen imports nothing upward); one-component-per-file satisfied (each route one default; each screen one named). Commit:
```powershell
git add expo
git commit -m "Add settings screen, route, and home affordance for account deletion"
```

---

### Task 4: Doctor check + config + contract docs

**Files:** Modify `scripts/submission-doctor.js`, `.env.example`, `SUBMISSION.md`, `CLAUDE.md`, `README.md`

- [ ] **Step 1: Add the doctor check**

In `scripts/submission-doctor.js`, immediately AFTER the `check('Apple Sign In configured when auth is enabled', ...)` block and BEFORE the `for (const result of checks)` loop, add:
```js
check('account deletion endpoint set when auth is enabled', () => {
  const authOn = Boolean(process.env.EXPO_PUBLIC_AUTH0_DOMAIN) && Boolean(process.env.EXPO_PUBLIC_AUTH0_CLIENT_ID);
  if (!authOn) {
    return true;
  }
  return (
    Boolean(process.env.EXPO_PUBLIC_ACCOUNT_DELETE_URL) ||
    'EXPO_PUBLIC_ACCOUNT_DELETE_URL required when auth is enabled (Apple 5.1.1(v) requires in-app account deletion)'
  );
});
```

- [ ] **Step 2: Verify the check behaves**

Run (in `expo/templates/app`): `npm run doctor` → still exits 1 (fresh scaffold); the new check shows PASS (auth off → skipped). Then:
```powershell
$env:EXPO_PUBLIC_AUTH0_DOMAIN='d'; $env:EXPO_PUBLIC_AUTH0_CLIENT_ID='c'; npm run doctor; Remove-Item Env:EXPO_PUBLIC_AUTH0_DOMAIN; Remove-Item Env:EXPO_PUBLIC_AUTH0_CLIENT_ID
```
Expected: a FAIL line `account deletion endpoint set when auth is enabled — EXPO_PUBLIC_ACCOUNT_DELETE_URL required when auth is enabled (Apple 5.1.1(v) requires in-app account deletion)`.

- [ ] **Step 3: Add the env var to `.env.example`** (append):
```
EXPO_PUBLIC_ACCOUNT_DELETE_URL=
```

- [ ] **Step 4: Update `SUBMISSION.md`**

In the `## Recorded values` table, add after the `Auth0 callback URL` row:
```
| Account delete endpoint | _unset_ |
```
In `## Stage 1 — Local readiness`, add after the existing auth lines:
```
- [ ] (If using auth) Account deletion endpoint deployed and EXPO_PUBLIC_ACCOUNT_DELETE_URL set (Apple 5.1.1(v) requires in-app account deletion)
```

- [ ] **Step 5: Update `CLAUDE.md`**

In the auth Architecture bullet (the one starting "Auth: `src/lib/auth/`"), append this sentence at the end of that bullet:
```markdown
  Account deletion (`useDeleteAccount`) calls `EXPO_PUBLIC_ACCOUNT_DELETE_URL`
  with the user's bearer token and then signs out; the backend performs the
  privileged Auth0 Management API delete (Apple 5.1.1(v)).
```

- [ ] **Step 6: Add the endpoint contract to `README.md`**

After the "## Continuous integration" section (or at the end of the App Store submission area — place it after the "## App Store submission" section), append:
```markdown
## Account deletion endpoint

Apple requires in-app account deletion (Guideline 5.1.1(v)) for apps with
accounts. The app ships the full client flow (Settings → Delete account →
confirm). The actual deletion runs on YOUR backend, because deleting an Auth0
user needs the Management API client secret, which must never be in the app.

Set `EXPO_PUBLIC_ACCOUNT_DELETE_URL` to an endpoint that:

1. Accepts `DELETE` with header `Authorization: Bearer <user access token>`.
2. Verifies the token against the Auth0 JWKS and extracts the `sub` claim.
3. Calls the Auth0 Management API to delete that user.
4. Returns `204` on success.

Reference (Node, illustrative — not shipped):

```js
import { createRemoteJWKSet, jwtVerify } from 'jose';

const jwks = createRemoteJWKSet(new URL(`https://${AUTH0_DOMAIN}/.well-known/jwks.json`));

export async function handleDelete(request) {
  const token = request.headers.authorization?.replace('Bearer ', '');
  const { payload } = await jwtVerify(token, jwks, { issuer: `https://${AUTH0_DOMAIN}/` });
  await fetch(`https://${AUTH0_DOMAIN}/api/v2/users/${encodeURIComponent(payload.sub)}`, {
    method: 'DELETE',
    headers: { Authorization: `Bearer ${MANAGEMENT_API_TOKEN}` }
  });
  return new Response(null, { status: 204 });
}
```
```

- [ ] **Step 7: Commit**

```powershell
git add expo
git commit -m "Add deletion doctor check, env var, and endpoint contract docs"
```

---

### Task 5: Validation assertion + CI

**Files:** Modify `expo/template-tests/scaffold-and-validate.ps1`

- [ ] **Step 1: Add the assertion**

In `expo/template-tests/scaffold-and-validate.ps1`, inside the `try`, after the "expo-router root layout must ship" block and before the closing `}` of the `try`, insert:
```powershell
    Write-Host "Doctor must flag missing account-deletion endpoint when auth is enabled..."
    $env:EXPO_PUBLIC_AUTH0_DOMAIN = 'smoke.auth0.com'
    $env:EXPO_PUBLIC_AUTH0_CLIENT_ID = 'smoke-client'
    $deleteDoctorOut = node scripts/submission-doctor.js 2>&1 | Out-String
    Remove-Item Env:EXPO_PUBLIC_AUTH0_DOMAIN
    Remove-Item Env:EXPO_PUBLIC_AUTH0_CLIENT_ID
    if ($deleteDoctorOut -notmatch 'EXPO_PUBLIC_ACCOUNT_DELETE_URL required when auth is enabled') {
        throw 'submission-doctor did not flag the missing account-deletion endpoint'
    }
```
Note: `$PSNativeCommandUseErrorActionPreference` is already disabled at the top of the script, so `node` exiting 1 here is captured into `$deleteDoctorOut` rather than throwing.

- [ ] **Step 2: Parse-check**

Run from repo root:
```powershell
$null = [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path .\expo\template-tests\scaffold-and-validate.ps1), [ref]$null, [ref]$null); if ($?) { 'parse ok' }
```
Expected: `parse ok`.

- [ ] **Step 3: Commit, push, watch CI**

```powershell
git add expo
git commit -m "Assert doctor flags missing account-deletion endpoint"
git push
gh run list --limit 1
```
Then `gh run watch <id> --exit-status`. Report all three jobs (`scaffold-and-test (cli)`, `scaffold-and-test (etl-api)`, `expo-scaffold-and-validate`) — all must be green. The expo job scaffolds a fresh app with the settings flow and runs the full guardrail set + the new deletion-endpoint assertion. On failure, `gh run view <id> --log-failed`, diagnose, fix.

---

## Self-review notes (already applied)

- **Spec coverage:** getAccountDeleteUrl + requestAccountDeletion (T1); useDeleteAccount with all branches incl. no-false-deletion-on-failure (T2); SettingsScreen two-step confirm + settings route + home affordance + home wiring (T3); doctor FAIL + env + SUBMISSION + CLAUDE + README contract (T4); validation assertion + CI (T5). AuthState unchanged (T2 composes getToken/signOut) — no test-helper churn beyond the new useDeleteAccount/Settings helpers.
- **Identifier consistency:** `EXPO_PUBLIC_ACCOUNT_DELETE_URL` identical across authConfig, doctor, .env.example, SUBMISSION.md, CLAUDE.md, README, and the validation assertion. `getAccountDeleteUrl`/`requestAccountDeletion`/`useDeleteAccount`/`canDeleteAccount`/`deleteAccount` names consistent T1→T3. `SettingsScreen` props match between screen, test, and route.
- **Placeholder scan:** none.
- **Gating:** every new gated file (`useDeleteAccount.ts`, `SettingsScreen.tsx`) has a colocated test; route files (`settings.tsx`) and home/settings glue are in `src/app` (untested). Doctor check is skipped (PASS) when auth is off, so no-auth scaffolds stay green.
