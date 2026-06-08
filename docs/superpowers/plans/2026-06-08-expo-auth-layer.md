# Expo Template Auth Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a config-gated Auth0 + native Apple Sign In auth layer to the Expo template (`src/lib/auth/` + sign-in screen + gate), auto-linked to the existing RevenueCat layer, with submission-layer support — all under the existing error-severity guardrails.

**Architecture:** A new `src/lib/auth/` mirrors `src/lib/purchases/`. It always ships but is inert until `EXPO_PUBLIC_AUTH0_DOMAIN` + `EXPO_PUBLIC_AUTH0_CLIENT_ID` are set. Pure auth-provider calls live in `authClient.ts` (unit-tested against mocked `expo-auth-session` + `fetch`); the `useAuth` hook orchestrates them with secure token storage; an `AuthGate` decides whether to show `SignInScreen`. A one-line bridge syncs the signed-in user id into RevenueCat.

**Tech Stack:** expo-auth-session (Auth0 OIDC + PKCE), expo-apple-authentication (native Apple button), expo-secure-store (token persistence), expo-crypto + expo-web-browser (auth-session peers), jest + @testing-library/react-native.

**Spec:** `docs/superpowers/specs/2026-06-08-expo-auth-layer-design.md`

**Working notes for the implementer:**
- All commands run inside `expo/templates/app/`. The harness repo has no pre-commit hooks; commit directly to `main`.
- **NO COMMENTS** in any `.ts`/`.tsx`/`.js` source. Test `code:` strings and bash `#` lines are exempt. Every code block below is comment-free — keep it so.
- Guardrails that will bite: `local/no-comments`, `@typescript-eslint/no-explicit-any` (error), `max-lines-per-function` (60), `max-params` (4), `one-component-per-file`, dependency-cruiser layering (`src/lib` must not import `src/app|features|components`), `check-test-files` (every `src/lib` module needs `__tests__/<name>.test.ts(x)`), jest coverage ≥80% lines/branches on `src/lib`.
- **expo-auth-session API is the one real risk.** Write the calls as shown, then run `npx tsc --noEmit`. The installed `expo-auth-session` type definitions are the source of truth — if a signature differs from the code below, adjust the call to match the installed types while keeping behavior identical. Never silence a mismatch with `any` or `eslint-disable` (both fail the build anyway).
- **Deliberate scope decision (Apple identity):** native Apple Sign In yields its own `TokenSet` (the Apple identity token) and `AuthUser`; it is NOT round-tripped through an Auth0 token-exchange. Unifying both providers under a single Auth0 identity needs a server exchange, which the spec defers to per-app backend work ("implementation decides how the token is used on the backend"). `getToken()` returns whichever session's token is active. This is called out again in the self-review.

---

## File structure (end state, additions only)

```
expo/templates/app/
  package.json                              ← + 5 expo deps (Task 1)
  jest.setup.js                             ← + 4 mocks, extend purchases mock (Task 1)
  app.config.js                             ← conditional auth env validation + apple plugin (Task 8)
  .env.example                              ← + 2 auth vars (Task 8)
  scripts/submission-doctor.js              ← + auth checks (Task 9)
  SUBMISSION.md                             ← Stage 1 auth lines (Task 10)
  CLAUDE.md                                 ← auth architecture + auth-setup routing (Task 10)
  src/lib/auth/
    authConfig.ts                           ← isAuthEnabled() + getAuthConfig()        (Task 2)
    tokenStore.ts                           ← TokenSet secure-store wrapper            (Task 3)
    authClient.ts                           ← Auth0 OIDC + userinfo + refresh          (Task 4)
    useAuth.ts                              ← the hook (AuthState)                     (Task 5)
    linkPurchases.ts                        ← syncPurchasesIdentity bridge             (Task 6)
    __tests__/authConfig.test.ts
    __tests__/tokenStore.test.ts
    __tests__/authClient.test.ts
    __tests__/useAuth.test.tsx
    __tests__/linkPurchases.test.ts
  src/app/
    SignInScreen.tsx                        ← Auth0 + Apple buttons                    (Task 7)
    AuthGate.tsx                            ← gating component                         (Task 7)
    __tests__/AuthGate.test.tsx
  App.tsx                                   ← wire useAuth + AuthGate + bridge         (Task 7)
  .claude/skills/auth-setup/SKILL.md        ← Playwright Auth0 dashboard skill         (Task 11)
  template-tests/scaffold-and-validate.ps1  ← + partial-auth-config assertion          (Task 12)
```

Shared types (defined once, imported elsewhere): `TokenSet` in `tokenStore.ts`; `AuthUser` in `authClient.ts`; `AuthConfig` in `authConfig.ts`; `AuthState` in `useAuth.ts`.

---

### Task 1: Dependencies + jest mocks

**Files:**
- Modify: `expo/templates/app/package.json` (deps)
- Modify: `expo/templates/app/jest.setup.js`

- [ ] **Step 1: Install the auth dependencies**

Run (in `expo/templates/app`):
```powershell
npx expo install expo-auth-session expo-apple-authentication expo-secure-store expo-crypto expo-web-browser
```
Expected: all five added to `package.json` dependencies at expo-managed versions. Keep whatever versions it writes.

- [ ] **Step 2: Replace `jest.setup.js` with the extended mock set**

```js
jest.mock('react-native-purchases', () => ({
  __esModule: true,
  default: {
    configure: jest.fn(),
    getCustomerInfo: jest.fn().mockResolvedValue({ entitlements: { active: {} } }),
    addCustomerInfoUpdateListener: jest.fn(),
    getOfferings: jest.fn().mockResolvedValue({ current: null }),
    purchasePackage: jest.fn(),
    logIn: jest.fn().mockResolvedValue({}),
    logOut: jest.fn().mockResolvedValue({})
  }
}));

jest.mock('expo-secure-store', () => {
  const store = {};
  return {
    setItemAsync: jest.fn(async (key, value) => {
      store[key] = value;
    }),
    getItemAsync: jest.fn(async (key) => (key in store ? store[key] : null)),
    deleteItemAsync: jest.fn(async (key) => {
      delete store[key];
    })
  };
});

jest.mock('expo-auth-session', () => ({
  makeRedirectUri: jest.fn(() => 'apptemplate://redirect'),
  fetchDiscoveryAsync: jest.fn().mockResolvedValue({
    authorizationEndpoint: 'https://tenant.auth0.com/authorize',
    tokenEndpoint: 'https://tenant.auth0.com/oauth/token'
  }),
  exchangeCodeAsync: jest.fn().mockResolvedValue({
    accessToken: 'access-1',
    refreshToken: 'refresh-1',
    idToken: 'id-1',
    expiresIn: 3600
  }),
  refreshAsync: jest.fn().mockResolvedValue({
    accessToken: 'access-2',
    refreshToken: 'refresh-2',
    idToken: 'id-2',
    expiresIn: 3600
  }),
  AuthRequest: class {
    constructor(options) {
      this.options = options;
      this.codeVerifier = 'verifier-1';
    }
    async promptAsync() {
      return { type: 'success', params: { code: 'code-1' } };
    }
  }
}));

jest.mock('expo-apple-authentication', () => ({
  signInAsync: jest.fn(),
  isAvailableAsync: jest.fn().mockResolvedValue(true),
  AppleAuthenticationScope: { FULL_NAME: 0, EMAIL: 1 },
  AppleAuthenticationButtonType: { SIGN_IN: 0 },
  AppleAuthenticationButtonStyle: { BLACK: 0 },
  AppleAuthenticationButton: 'AppleAuthenticationButton'
}));
```

- [ ] **Step 3: Verify existing tests still pass**

Run: `npx jest --silent`
Expected: the existing suites (purchases, screens, eslint-rules) still pass — the extended purchases mock is additive.

- [ ] **Step 4: Commit**

```powershell
git add expo
git commit -m "Add auth dependencies and jest mocks for auth modules"
```

---

### Task 2: authConfig (TDD)

**Files:**
- Create: `expo/templates/app/src/lib/auth/__tests__/authConfig.test.ts`, `src/lib/auth/authConfig.ts`

- [ ] **Step 1: Write the failing test**

`src/lib/auth/__tests__/authConfig.test.ts`:
```ts
import { getAuthConfig, isAuthEnabled } from '../authConfig';

describe('authConfig', () => {
  beforeEach(() => {
    delete process.env.EXPO_PUBLIC_AUTH0_DOMAIN;
    delete process.env.EXPO_PUBLIC_AUTH0_CLIENT_ID;
  });

  test('isAuthEnabled is false when both vars are unset', () => {
    expect(isAuthEnabled()).toBe(false);
    expect(getAuthConfig()).toBeNull();
  });

  test('isAuthEnabled is false when only one var is set', () => {
    process.env.EXPO_PUBLIC_AUTH0_DOMAIN = 'tenant.auth0.com';
    expect(isAuthEnabled()).toBe(false);
    expect(getAuthConfig()).toBeNull();
  });

  test('returns config when both vars are set', () => {
    process.env.EXPO_PUBLIC_AUTH0_DOMAIN = 'tenant.auth0.com';
    process.env.EXPO_PUBLIC_AUTH0_CLIENT_ID = 'client-123';
    expect(isAuthEnabled()).toBe(true);
    expect(getAuthConfig()).toEqual({ domain: 'tenant.auth0.com', clientId: 'client-123' });
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `npx jest src/lib/auth/__tests__/authConfig`
Expected: FAIL — cannot find `../authConfig`.

- [ ] **Step 3: Implement `src/lib/auth/authConfig.ts`**

```ts
export type AuthConfig = {
  domain: string;
  clientId: string;
};

export function getAuthConfig(): AuthConfig | null {
  const domain = process.env.EXPO_PUBLIC_AUTH0_DOMAIN ?? '';
  const clientId = process.env.EXPO_PUBLIC_AUTH0_CLIENT_ID ?? '';
  if (domain === '' || clientId === '') {
    return null;
  }
  return { domain, clientId };
}

export function isAuthEnabled(): boolean {
  return getAuthConfig() !== null;
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `npx jest src/lib/auth/__tests__/authConfig`
Expected: PASS, 3 tests.

- [ ] **Step 5: Commit**

```powershell
git add expo
git commit -m "Add auth config gating"
```

---

### Task 3: tokenStore (TDD)

**Files:**
- Create: `expo/templates/app/src/lib/auth/__tests__/tokenStore.test.ts`, `src/lib/auth/tokenStore.ts`

- [ ] **Step 1: Write the failing test**

`src/lib/auth/__tests__/tokenStore.test.ts`:
```ts
import { clearTokens, loadTokens, saveTokens, TokenSet } from '../tokenStore';

const sample: TokenSet = {
  accessToken: 'access',
  refreshToken: 'refresh',
  idToken: 'id',
  expiresAt: 1000
};

describe('tokenStore', () => {
  beforeEach(async () => {
    await clearTokens();
  });

  test('returns null when nothing is stored', async () => {
    expect(await loadTokens()).toBeNull();
  });

  test('round-trips a saved token set', async () => {
    await saveTokens(sample);
    expect(await loadTokens()).toEqual(sample);
  });

  test('clear removes the stored tokens', async () => {
    await saveTokens(sample);
    await clearTokens();
    expect(await loadTokens()).toBeNull();
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `npx jest src/lib/auth/__tests__/tokenStore`
Expected: FAIL — cannot find `../tokenStore`.

- [ ] **Step 3: Implement `src/lib/auth/tokenStore.ts`**

```ts
import * as SecureStore from 'expo-secure-store';

export type TokenSet = {
  accessToken: string;
  refreshToken: string | null;
  idToken: string | null;
  expiresAt: number;
};

const STORAGE_KEY = 'auth.tokens';

export async function saveTokens(tokens: TokenSet): Promise<void> {
  await SecureStore.setItemAsync(STORAGE_KEY, JSON.stringify(tokens));
}

export async function loadTokens(): Promise<TokenSet | null> {
  const raw = await SecureStore.getItemAsync(STORAGE_KEY);
  if (raw === null) {
    return null;
  }
  return JSON.parse(raw) as TokenSet;
}

export async function clearTokens(): Promise<void> {
  await SecureStore.deleteItemAsync(STORAGE_KEY);
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `npx jest src/lib/auth/__tests__/tokenStore`
Expected: PASS, 3 tests.

- [ ] **Step 5: Commit**

```powershell
git add expo
git commit -m "Add secure token store"
```

---

### Task 4: authClient — Auth0 OIDC (TDD)

**Files:**
- Create: `expo/templates/app/src/lib/auth/__tests__/authClient.test.ts`, `src/lib/auth/authClient.ts`

This is the file most likely to need reconciliation against the installed `expo-auth-session` types. Write it as shown, then let `npx tsc --noEmit` (Task 7 / verify) drive any signature corrections.

- [ ] **Step 1: Write the failing test**

`src/lib/auth/__tests__/authClient.test.ts`:
```ts
import * as AuthSession from 'expo-auth-session';

import { fetchUserInfo, isExpired, loginWithAuth0, refreshTokens } from '../authClient';

const config = { domain: 'tenant.auth0.com', clientId: 'client-123' };

describe('isExpired', () => {
  test('false when expiry is safely in the future', () => {
    expect(isExpired({ accessToken: 'a', refreshToken: null, idToken: null, expiresAt: 10_000_000 }, 1000)).toBe(false);
  });

  test('true within the 60s skew window', () => {
    expect(isExpired({ accessToken: 'a', refreshToken: null, idToken: null, expiresAt: 50_000 }, 0)).toBe(true);
  });
});

describe('fetchUserInfo', () => {
  test('maps the userinfo response to an AuthUser', async () => {
    global.fetch = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ sub: 'auth0|42', email: 'a@b.com', name: 'Ada' })
    }) as unknown as typeof fetch;
    const user = await fetchUserInfo('tenant.auth0.com', 'access');
    expect(user).toEqual({ id: 'auth0|42', email: 'a@b.com', name: 'Ada' });
    expect(global.fetch).toHaveBeenCalledWith('https://tenant.auth0.com/userinfo', {
      headers: { Authorization: 'Bearer access' }
    });
  });

  test('null email and name when absent', async () => {
    global.fetch = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ sub: 'auth0|7' })
    }) as unknown as typeof fetch;
    expect(await fetchUserInfo('tenant.auth0.com', 'access')).toEqual({ id: 'auth0|7', email: null, name: null });
  });
});

describe('loginWithAuth0', () => {
  test('exchanges the code and returns a token set', async () => {
    const tokens = await loginWithAuth0(config, 1000);
    expect(tokens).toEqual({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      idToken: 'id-1',
      expiresAt: 1000 + 3600 * 1000
    });
    expect(AuthSession.exchangeCodeAsync).toHaveBeenCalled();
  });

  test('returns null when the prompt is dismissed', async () => {
    const original = AuthSession.AuthRequest;
    (AuthSession as { AuthRequest: unknown }).AuthRequest = class {
      codeVerifier = 'v';
      async promptAsync() {
        return { type: 'dismiss' };
      }
    };
    expect(await loginWithAuth0(config, 1000)).toBeNull();
    (AuthSession as { AuthRequest: unknown }).AuthRequest = original;
  });
});

describe('refreshTokens', () => {
  test('refreshes and maps the new token set', async () => {
    const tokens = await refreshTokens(config, 'refresh-1', 2000);
    expect(tokens).toEqual({
      accessToken: 'access-2',
      refreshToken: 'refresh-2',
      idToken: 'id-2',
      expiresAt: 2000 + 3600 * 1000
    });
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `npx jest src/lib/auth/__tests__/authClient`
Expected: FAIL — cannot find `../authClient`.

- [ ] **Step 3: Implement `src/lib/auth/authClient.ts`**

```ts
import * as AuthSession from 'expo-auth-session';

import { AuthConfig } from './authConfig';
import { TokenSet } from './tokenStore';

export type AuthUser = {
  id: string;
  email: string | null;
  name: string | null;
};

const EXPIRY_SKEW_MS = 60_000;

export function isExpired(tokens: TokenSet, nowMs: number): boolean {
  return nowMs >= tokens.expiresAt - EXPIRY_SKEW_MS;
}

function toTokenSet(response: AuthSession.TokenResponse, nowMs: number): TokenSet {
  return {
    accessToken: response.accessToken,
    refreshToken: response.refreshToken ?? null,
    idToken: response.idToken ?? null,
    expiresAt: nowMs + (response.expiresIn ?? 0) * 1000
  };
}

export async function fetchUserInfo(domain: string, accessToken: string): Promise<AuthUser> {
  const response = await fetch(`https://${domain}/userinfo`, {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  const profile = (await response.json()) as { sub: string; email?: string; name?: string };
  return {
    id: profile.sub,
    email: profile.email ?? null,
    name: profile.name ?? null
  };
}

export async function loginWithAuth0(config: AuthConfig, nowMs: number): Promise<TokenSet | null> {
  const discovery = await AuthSession.fetchDiscoveryAsync(`https://${config.domain}`);
  const redirectUri = AuthSession.makeRedirectUri();
  const request = new AuthSession.AuthRequest({
    clientId: config.clientId,
    redirectUri,
    scopes: ['openid', 'profile', 'email', 'offline_access'],
    usePKCE: true
  });
  const result = await request.promptAsync(discovery);
  if (result.type !== 'success') {
    return null;
  }
  const response = await AuthSession.exchangeCodeAsync(
    {
      clientId: config.clientId,
      code: result.params.code,
      redirectUri,
      extraParams: { code_verifier: request.codeVerifier ?? '' }
    },
    discovery
  );
  return toTokenSet(response, nowMs);
}

export async function refreshTokens(config: AuthConfig, refreshToken: string, nowMs: number): Promise<TokenSet> {
  const discovery = await AuthSession.fetchDiscoveryAsync(`https://${config.domain}`);
  const response = await AuthSession.refreshAsync(
    { clientId: config.clientId, refreshToken },
    discovery
  );
  return toTokenSet(response, nowMs);
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `npx jest src/lib/auth/__tests__/authClient`
Expected: PASS. If `npx tsc --noEmit` later flags a signature (e.g. `exchangeCodeAsync`/`refreshAsync`/`AuthRequest` options differ in the installed version), adjust the call to the installed type — keep the same inputs/outputs and keep the tests green.

- [ ] **Step 5: Commit**

```powershell
git add expo
git commit -m "Add Auth0 OIDC client functions"
```

---

### Task 5: useAuth hook (TDD)

**Files:**
- Create: `expo/templates/app/src/lib/auth/__tests__/useAuth.test.tsx`, `src/lib/auth/useAuth.ts`

The hook mocks `./authClient`, `./tokenStore`, and `expo-apple-authentication` so the test exercises orchestration, not the provider calls (covered in Task 4).

- [ ] **Step 1: Write the failing test**

`src/lib/auth/__tests__/useAuth.test.tsx`:
```tsx
import { act, renderHook, waitFor } from '@testing-library/react-native';
import * as AppleAuthentication from 'expo-apple-authentication';

import * as authClient from '../authClient';
import * as tokenStore from '../tokenStore';
import { useAuth } from '../useAuth';

jest.mock('../authClient');
jest.mock('../tokenStore');

const mockedClient = authClient as jest.Mocked<typeof authClient>;
const mockedStore = tokenStore as jest.Mocked<typeof tokenStore>;
const mockedApple = AppleAuthentication as jest.Mocked<typeof AppleAuthentication>;

const validTokens = { accessToken: 'a', refreshToken: 'r', idToken: 'i', expiresAt: 9_999_999_999_999 };
const ada = { id: 'auth0|1', email: 'a@b.com', name: 'Ada' };

function enableAuth() {
  process.env.EXPO_PUBLIC_AUTH0_DOMAIN = 'tenant.auth0.com';
  process.env.EXPO_PUBLIC_AUTH0_CLIENT_ID = 'client-123';
}

describe('useAuth', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    delete process.env.EXPO_PUBLIC_AUTH0_DOMAIN;
    delete process.env.EXPO_PUBLIC_AUTH0_CLIENT_ID;
    mockedStore.loadTokens.mockResolvedValue(null);
    mockedClient.isExpired.mockReturnValue(false);
  });

  test('inert when auth is disabled', async () => {
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isAuthEnabled).toBe(false);
    expect(result.current.isAuthenticated).toBe(false);
    expect(await result.current.getToken()).toBeNull();
    expect(mockedClient.loginWithAuth0).not.toHaveBeenCalled();
  });

  test('restores a valid stored session on mount', async () => {
    enableAuth();
    mockedStore.loadTokens.mockResolvedValue(validTokens);
    mockedClient.fetchUserInfo.mockResolvedValue(ada);
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isAuthenticated).toBe(true));
    expect(result.current.user).toEqual(ada);
  });

  test('refreshes an expired stored session', async () => {
    enableAuth();
    mockedStore.loadTokens.mockResolvedValue({ ...validTokens, expiresAt: 1 });
    mockedClient.isExpired.mockReturnValue(true);
    mockedClient.refreshTokens.mockResolvedValue(validTokens);
    mockedClient.fetchUserInfo.mockResolvedValue(ada);
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isAuthenticated).toBe(true));
    expect(mockedClient.refreshTokens).toHaveBeenCalled();
  });

  test('signIn logs in, stores tokens, and loads the user', async () => {
    enableAuth();
    mockedClient.loginWithAuth0.mockResolvedValue(validTokens);
    mockedClient.fetchUserInfo.mockResolvedValue(ada);
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    await act(async () => {
      await result.current.signIn();
    });
    expect(mockedStore.saveTokens).toHaveBeenCalledWith(validTokens);
    expect(result.current.isAuthenticated).toBe(true);
    expect(result.current.user).toEqual(ada);
  });

  test('signInWithApple sets the user from the Apple credential', async () => {
    enableAuth();
    mockedApple.signInAsync.mockResolvedValue({
      user: 'apple-9',
      email: 'a@icloud.com',
      fullName: { givenName: 'Ada', familyName: 'Lovelace' },
      identityToken: 'apple-id-token'
    } as Awaited<ReturnType<typeof AppleAuthentication.signInAsync>>);
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    await act(async () => {
      await result.current.signInWithApple();
    });
    expect(result.current.isAuthenticated).toBe(true);
    expect(result.current.user).toEqual({ id: 'apple-9', email: 'a@icloud.com', name: 'Ada Lovelace' });
  });

  test('signOut clears tokens and user', async () => {
    enableAuth();
    mockedStore.loadTokens.mockResolvedValue(validTokens);
    mockedClient.fetchUserInfo.mockResolvedValue(ada);
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isAuthenticated).toBe(true));
    await act(async () => {
      await result.current.signOut();
    });
    expect(mockedStore.clearTokens).toHaveBeenCalled();
    expect(result.current.isAuthenticated).toBe(false);
    expect(result.current.user).toBeNull();
  });

  test('getToken returns the access token when valid', async () => {
    enableAuth();
    mockedStore.loadTokens.mockResolvedValue(validTokens);
    mockedClient.fetchUserInfo.mockResolvedValue(ada);
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isAuthenticated).toBe(true));
    expect(await result.current.getToken()).toBe('a');
  });

  test('getToken refreshes when the token is expired', async () => {
    enableAuth();
    mockedStore.loadTokens.mockResolvedValue(validTokens);
    mockedClient.fetchUserInfo.mockResolvedValue(ada);
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isAuthenticated).toBe(true));
    mockedClient.isExpired.mockReturnValue(true);
    mockedClient.refreshTokens.mockResolvedValue({ ...validTokens, accessToken: 'fresh' });
    expect(await result.current.getToken()).toBe('fresh');
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `npx jest src/lib/auth/__tests__/useAuth`
Expected: FAIL — cannot find `../useAuth`.

- [ ] **Step 3: Implement `src/lib/auth/useAuth.ts`**

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import * as AppleAuthentication from 'expo-apple-authentication';

import { AuthUser, fetchUserInfo, isExpired, loginWithAuth0, refreshTokens } from './authClient';
import { getAuthConfig } from './authConfig';
import { clearTokens, loadTokens, saveTokens, TokenSet } from './tokenStore';

export type AuthState = {
  isAuthEnabled: boolean;
  isAuthenticated: boolean;
  isLoading: boolean;
  user: AuthUser | null;
  signIn: () => Promise<void>;
  signInWithApple: () => Promise<void>;
  signOut: () => Promise<void>;
  getToken: () => Promise<string | null>;
};

function appleName(fullName: AppleAuthentication.AppleAuthenticationFullName | null): string | null {
  if (!fullName) {
    return null;
  }
  const parts = [fullName.givenName, fullName.familyName].filter(Boolean);
  return parts.length > 0 ? parts.join(' ') : null;
}

export function useAuth(): AuthState {
  const config = getAuthConfig();
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(config !== null);
  const tokensRef = useRef<TokenSet | null>(null);

  const adopt = useCallback(
    async (tokens: TokenSet, domain: string) => {
      tokensRef.current = tokens;
      await saveTokens(tokens);
      const profile = await fetchUserInfo(domain, tokens.accessToken);
      setUser(profile);
      setIsAuthenticated(true);
    },
    []
  );

  useEffect(() => {
    if (config === null) {
      return;
    }
    let active = true;
    const restore = async () => {
      try {
        const stored = await loadTokens();
        if (!stored) {
          return;
        }
        tokensRef.current = stored;
        let usable = stored;
        if (isExpired(stored, Date.now())) {
          if (!stored.refreshToken) {
            await clearTokens();
            return;
          }
          usable = await refreshTokens(config, stored.refreshToken, Date.now());
        }
        if (active) {
          await adopt(usable, config.domain);
        }
      } catch {
        await clearTokens();
      } finally {
        if (active) {
          setIsLoading(false);
        }
      }
    };
    restore();
    return () => {
      active = false;
    };
  }, [config, adopt]);

  const signIn = useCallback(async () => {
    if (config === null) {
      return;
    }
    const tokens = await loginWithAuth0(config, Date.now());
    if (!tokens) {
      return;
    }
    await adopt(tokens, config.domain);
  }, [config, adopt]);

  const signInWithApple = useCallback(async () => {
    if (config === null) {
      return;
    }
    try {
      const credential = await AppleAuthentication.signInAsync({
        requestedScopes: [
          AppleAuthentication.AppleAuthenticationScope.FULL_NAME,
          AppleAuthentication.AppleAuthenticationScope.EMAIL
        ]
      });
      const tokens: TokenSet = {
        accessToken: credential.identityToken ?? '',
        refreshToken: null,
        idToken: credential.identityToken ?? null,
        expiresAt: Date.now() + 3600 * 1000
      };
      tokensRef.current = tokens;
      await saveTokens(tokens);
      setUser({ id: credential.user, email: credential.email ?? null, name: appleName(credential.fullName) });
      setIsAuthenticated(true);
    } catch {
      return;
    }
  }, [config]);

  const signOut = useCallback(async () => {
    await clearTokens();
    tokensRef.current = null;
    setUser(null);
    setIsAuthenticated(false);
  }, []);

  const getToken = useCallback(async () => {
    const current = tokensRef.current;
    if (config === null || !current) {
      return null;
    }
    if (!isExpired(current, Date.now())) {
      return current.accessToken;
    }
    if (!current.refreshToken) {
      return null;
    }
    const refreshed = await refreshTokens(config, current.refreshToken, Date.now());
    tokensRef.current = refreshed;
    await saveTokens(refreshed);
    return refreshed.accessToken;
  }, [config]);

  return {
    isAuthEnabled: config !== null,
    isAuthenticated,
    isLoading,
    user,
    signIn,
    signInWithApple,
    signOut,
    getToken
  };
}
```

- [ ] **Step 4: Run to verify it passes + coverage**

Run: `npx jest src/lib/auth --coverage --collectCoverageFrom="src/lib/auth/**/*.ts"`
Expected: PASS. `authConfig`, `tokenStore`, `authClient`, `useAuth` each ≥80% lines/branches. If a branch is uncovered (e.g. the `getToken` no-refresh-token path or the restore `catch`), add a focused test for it before moving on.

- [ ] **Step 5: Commit**

```powershell
git add expo
git commit -m "Add useAuth hook orchestrating Auth0 and Apple sign-in"
```

---

### Task 6: linkPurchases bridge (TDD)

**Files:**
- Create: `expo/templates/app/src/lib/auth/__tests__/linkPurchases.test.ts`, `src/lib/auth/linkPurchases.ts`

- [ ] **Step 1: Write the failing test**

`src/lib/auth/__tests__/linkPurchases.test.ts`:
```ts
import Purchases from 'react-native-purchases';

import { syncPurchasesIdentity } from '../linkPurchases';

const mockedPurchases = Purchases as jest.Mocked<typeof Purchases>;

describe('syncPurchasesIdentity', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  test('logs in RevenueCat with the user id when a user is present', async () => {
    await syncPurchasesIdentity({ id: 'auth0|1', email: null, name: null });
    expect(mockedPurchases.logIn).toHaveBeenCalledWith('auth0|1');
    expect(mockedPurchases.logOut).not.toHaveBeenCalled();
  });

  test('logs out RevenueCat when the user is null', async () => {
    await syncPurchasesIdentity(null);
    expect(mockedPurchases.logOut).toHaveBeenCalled();
    expect(mockedPurchases.logIn).not.toHaveBeenCalled();
  });

  test('does not throw when RevenueCat rejects', async () => {
    mockedPurchases.logIn.mockRejectedValueOnce(new Error('not configured'));
    await expect(syncPurchasesIdentity({ id: 'auth0|1', email: null, name: null })).resolves.toBeUndefined();
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `npx jest src/lib/auth/__tests__/linkPurchases`
Expected: FAIL — cannot find `../linkPurchases`.

- [ ] **Step 3: Implement `src/lib/auth/linkPurchases.ts`**

```ts
import Purchases from 'react-native-purchases';

import { AuthUser } from './authClient';

export async function syncPurchasesIdentity(user: AuthUser | null): Promise<void> {
  const action = user ? Purchases.logIn(user.id) : Purchases.logOut();
  await action.then(
    () => undefined,
    () => undefined
  );
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `npx jest src/lib/auth/__tests__/linkPurchases`
Expected: PASS, 3 tests.

- [ ] **Step 5: Commit**

```powershell
git add expo
git commit -m "Add auth-to-RevenueCat identity bridge"
```

---

### Task 7: SignInScreen + AuthGate + wire App.tsx

**Files:**
- Create: `expo/templates/app/src/app/SignInScreen.tsx`, `src/app/AuthGate.tsx`, `src/app/__tests__/AuthGate.test.tsx`
- Modify: `expo/templates/app/App.tsx`

- [ ] **Step 1: Write the failing AuthGate test**

`src/app/__tests__/AuthGate.test.tsx`:
```tsx
import { render, screen } from '@testing-library/react-native';
import { Text } from 'react-native';

import { AuthGate } from '../AuthGate';
import type { AuthState } from '../../lib/auth/useAuth';

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

describe('AuthGate', () => {
  test('renders children when auth is disabled', () => {
    render(
      <AuthGate auth={authState({ isAuthEnabled: false })}>
        <Text>protected</Text>
      </AuthGate>
    );
    expect(screen.getByText('protected')).toBeTruthy();
  });

  test('renders the sign-in screen when enabled and unauthenticated', () => {
    render(
      <AuthGate auth={authState({ isAuthEnabled: true, isAuthenticated: false })}>
        <Text>protected</Text>
      </AuthGate>
    );
    expect(screen.queryByText('protected')).toBeNull();
    expect(screen.getByText('Continue')).toBeTruthy();
  });

  test('renders children when authenticated', () => {
    render(
      <AuthGate auth={authState({ isAuthEnabled: true, isAuthenticated: true })}>
        <Text>protected</Text>
      </AuthGate>
    );
    expect(screen.getByText('protected')).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `npx jest src/app/__tests__/AuthGate`
Expected: FAIL — cannot find `../AuthGate`.

- [ ] **Step 3: Implement the screen and gate**

`src/app/SignInScreen.tsx`:
```tsx
import * as AppleAuthentication from 'expo-apple-authentication';
import { ActivityIndicator, Platform, Pressable, StyleSheet, Text, View } from 'react-native';

import type { AuthState } from '../lib/auth/useAuth';

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

`src/app/AuthGate.tsx`:
```tsx
import { ReactNode } from 'react';

import { SignInScreen } from './SignInScreen';
import type { AuthState } from '../lib/auth/useAuth';

type AuthGateProps = {
  auth: AuthState;
  children: ReactNode;
};

export function AuthGate({ auth, children }: AuthGateProps) {
  if (auth.isAuthEnabled && !auth.isAuthenticated) {
    return <SignInScreen auth={auth} />;
  }
  return <>{children}</>;
}
```

- [ ] **Step 4: Wire `App.tsx`** (replace entirely)

```tsx
import { useEffect, useState } from 'react';
import { StatusBar } from 'expo-status-bar';

import { AuthGate } from './src/app/AuthGate';
import { HomeScreen } from './src/app/HomeScreen';
import { PaywallScreen } from './src/app/PaywallScreen';
import { syncPurchasesIdentity } from './src/lib/auth/linkPurchases';
import { useAuth } from './src/lib/auth/useAuth';
import { initPurchases } from './src/lib/purchases/initPurchases';
import { useSubscription } from './src/lib/purchases/useSubscription';

initPurchases();

export default function App() {
  const [showPaywall, setShowPaywall] = useState(false);
  const { isSubscribed } = useSubscription();
  const auth = useAuth();

  useEffect(() => {
    syncPurchasesIdentity(auth.user);
  }, [auth.user]);

  return (
    <AuthGate auth={auth}>
      {showPaywall && !isSubscribed ? (
        <PaywallScreen onClose={() => setShowPaywall(false)} />
      ) : (
        <HomeScreen isSubscribed={isSubscribed} onUpgradePress={() => setShowPaywall(true)} />
      )}
      <StatusBar style="light" />
    </AuthGate>
  );
}
```

- [ ] **Step 5: Run the full guardrail set**

Run: `npm run verify`
Expected: typecheck, lint, depcruise, check-test-files, jest --coverage all pass. This is where `expo-auth-session` signature mismatches surface in `tsc` — fix per installed types if any. dependency-cruiser must show no violations (the bridge imports `react-native-purchases`, not the purchases lib; `src/lib/auth` imports nothing from `src/app`).

- [ ] **Step 6: Commit**

```powershell
git add expo
git commit -m "Add sign-in screen and auth gate, wire app and purchases bridge"
```

---

### Task 8: app.config.js conditional validation + Apple plugin + .env.example

**Files:**
- Modify: `expo/templates/app/app.config.js`
- Modify: `expo/templates/app/.env.example`

- [ ] **Step 1: Replace `app.config.js`**

```js
if (process.env.NODE_ENV === 'production' && !process.env.EAS_BUILD) {
  require('dotenv').config({ path: '.env.production' });
}

const packageJson = require('./package.json');
const isProduction = process.env.NODE_ENV === 'production';

const REQUIRED_PRODUCTION_ENV_VARS = ['EXPO_PUBLIC_REVENUECAT_IOS_API_KEY'];
const AUTH_ENV_VARS = ['EXPO_PUBLIC_AUTH0_DOMAIN', 'EXPO_PUBLIC_AUTH0_CLIENT_ID'];

function missingProductionVars() {
  const missing = REQUIRED_PRODUCTION_ENV_VARS.filter((name) => !process.env[name]);
  const setAuthVars = AUTH_ENV_VARS.filter((name) => process.env[name]);
  if (setAuthVars.length > 0 && setAuthVars.length < AUTH_ENV_VARS.length) {
    missing.push(...AUTH_ENV_VARS.filter((name) => !process.env[name]));
  }
  return missing;
}

if (isProduction) {
  const missing = missingProductionVars();
  if (missing.length > 0) {
    throw new Error(`Missing required production env vars: ${missing.join(', ')}`);
  }
}

const bundleId = isProduction ? 'com.example.apptemplate' : 'com.example.apptemplate.dev';
const plugins = isProduction
  ? ['expo-apple-authentication']
  : ['expo-dev-client', 'expo-apple-authentication'];

module.exports = {
  expo: {
    name: isProduction ? 'AppTemplate' : 'AppTemplate Dev',
    slug: 'app-template',
    version: packageJson.version,
    orientation: 'portrait',
    userInterfaceStyle: 'automatic',
    assetBundlePatterns: ['**/*'],
    ios: {
      supportsTablet: false,
      bundleIdentifier: bundleId,
      infoPlist: {
        ITSAppUsesNonExemptEncryption: false
      }
    },
    android: {
      package: bundleId
    },
    plugins,
    scheme: 'apptemplate',
    extra: {}
  }
};
```

- [ ] **Step 2: Append the auth vars to `.env.example`**

Final `.env.example`:
```
EXPO_PUBLIC_REVENUECAT_IOS_API_KEY=
EXPO_PUBLIC_AUTH0_DOMAIN=
EXPO_PUBLIC_AUTH0_CLIENT_ID=
```

- [ ] **Step 3: Verify config loads in both modes**

Run (in `expo/templates/app`):
```powershell
node -e "process.env.NODE_ENV='development'; require('./app.config.js'); console.log('dev ok')"
$env:NODE_ENV='production'; $env:EXPO_PUBLIC_REVENUECAT_IOS_API_KEY='x'; node -e "require('./app.config.js'); console.log('prod ok')"; Remove-Item Env:NODE_ENV; Remove-Item Env:EXPO_PUBLIC_REVENUECAT_IOS_API_KEY
```
Expected: `dev ok` then `prod ok` (no auth vars set → auth not required). Then verify partial auth config throws:
```powershell
$env:NODE_ENV='production'; $env:EXPO_PUBLIC_REVENUECAT_IOS_API_KEY='x'; $env:EXPO_PUBLIC_AUTH0_DOMAIN='d'; node -e "try { require('./app.config.js'); console.log('NO THROW') } catch (e) { console.log('threw: ' + e.message) }"; Remove-Item Env:NODE_ENV; Remove-Item Env:EXPO_PUBLIC_REVENUECAT_IOS_API_KEY; Remove-Item Env:EXPO_PUBLIC_AUTH0_DOMAIN
```
Expected: `threw: Missing required production env vars: EXPO_PUBLIC_AUTH0_CLIENT_ID`.

- [ ] **Step 4: Run verify and commit**

Run: `npm run verify`
Expected: all gates pass.
```powershell
git add expo
git commit -m "Add conditional auth env validation and Apple Sign In plugin"
```

---

### Task 9: submission-doctor auth checks

**Files:**
- Modify: `expo/templates/app/scripts/submission-doctor.js`

- [ ] **Step 1: Add a plugin-name helper and two auth checks**

Insert this helper directly after the `let config = null;` line in `scripts/submission-doctor.js`:
```js
function pluginNames(cfg) {
  return (cfg.plugins || []).map((entry) => (Array.isArray(entry) ? entry[0] : entry));
}
```

Then add these two `check(...)` calls immediately before the final `for (const result of checks)` loop:
```js
check('auth env vars are all-or-nothing', () => {
  const set = ['EXPO_PUBLIC_AUTH0_DOMAIN', 'EXPO_PUBLIC_AUTH0_CLIENT_ID'].filter((name) => process.env[name]);
  return set.length === 0 || set.length === 2 || 'set EXPO_PUBLIC_AUTH0_DOMAIN and EXPO_PUBLIC_AUTH0_CLIENT_ID together';
});

check('Apple Sign In configured when auth is enabled', () => {
  const authOn = Boolean(process.env.EXPO_PUBLIC_AUTH0_DOMAIN) && Boolean(process.env.EXPO_PUBLIC_AUTH0_CLIENT_ID);
  if (!authOn) {
    return true;
  }
  if (!config) {
    return 'app.config.js did not load';
  }
  return (
    pluginNames(config).includes('expo-apple-authentication') ||
    'expo-apple-authentication plugin missing (Apple requires Sign in with Apple when offering social login)'
  );
});
```

- [ ] **Step 2: Verify doctor behavior**

Run (in `expo/templates/app`): `npm run doctor`
Expected: still exits 1 on the fresh template; the two new checks PASS (auth vars unset → all-or-nothing satisfied, Apple check skipped). Confirm `auth env vars are all-or-nothing` shows PASS.

Then confirm the all-or-nothing check fires:
```powershell
$env:EXPO_PUBLIC_AUTH0_DOMAIN='d'; npm run doctor; Remove-Item Env:EXPO_PUBLIC_AUTH0_DOMAIN
```
Expected: a FAIL line `auth env vars are all-or-nothing — set EXPO_PUBLIC_AUTH0_DOMAIN and EXPO_PUBLIC_AUTH0_CLIENT_ID together`. (Doctor still exits 1 regardless; you're checking the line is present.)

- [ ] **Step 3: Commit**

```powershell
git add expo
git commit -m "Add auth readiness checks to submission-doctor"
```

---

### Task 10: SUBMISSION.md + CLAUDE.md updates

**Files:**
- Modify: `expo/templates/app/SUBMISSION.md`
- Modify: `expo/templates/app/CLAUDE.md`

- [ ] **Step 1: Add auth to `SUBMISSION.md`**

In the `## Recorded values` table, add these rows after the `RevenueCat entitlement` row:
```
| Auth0 domain | _unset_ |
| Auth0 client ID | _unset_ |
| Auth0 callback URL | _unset_ |
```

In `## Stage 1 — Local readiness`, add these checklist items before the `npm run doctor passes` line:
```
- [ ] (If using auth) Auth0 tenant + native application configured (skill: auth-setup)
- [ ] (If using auth) Sign in with Apple enabled in Auth0 (required by Apple when any social login is offered)
- [ ] (If using auth) EXPO_PUBLIC_AUTH0_DOMAIN and EXPO_PUBLIC_AUTH0_CLIENT_ID set together in .env.production
```

- [ ] **Step 2: Update `CLAUDE.md`**

In `## Architecture`, add this bullet after the `Subscriptions:` bullet:
```
- Auth: `src/lib/auth/` wraps Auth0 (OIDC via expo-auth-session) and native
  Apple Sign In. It is inert until `EXPO_PUBLIC_AUTH0_DOMAIN` and
  `EXPO_PUBLIC_AUTH0_CLIENT_ID` are set. `useAuth()` exposes
  `getToken()` — the single accessor for attaching a bearer token to your
  backend calls. When auth and payments are both on, `App.tsx` calls
  `syncPurchasesIdentity` so RevenueCat entitlements follow the account.
```

In the `## App Store submission — orchestration` routing table, change the Stage 1 row from:
```
| 1 Local readiness | submission-doctor |
```
to:
```
| 1 Local readiness | submission-doctor; auth-setup (if using auth) |
```

- [ ] **Step 3: Commit**

```powershell
git add expo
git commit -m "Document auth in SUBMISSION.md and CLAUDE.md"
```

---

### Task 11: auth-setup skill

**Files:**
- Create: `expo/templates/app/.claude/skills/auth-setup/SKILL.md`

- [ ] **Step 1: Write `SKILL.md`**

```markdown
---
name: auth-setup
description: Use for Auth0 configuration - native application, callback/logout URLs, the Apple connection, and writing the public env vars (part of Stage 1 of SUBMISSION.md, only when the app uses auth)
---

# Auth0 Setup (Playwright-driven)

Drives https://manage.auth0.com via Playwright MCP browser tools. Only run this
when the app uses auth (the user wants a login).

## Preconditions

- Read SUBMISSION.md first.
- If a login screen appears, STOP and ask the user to log in, then continue.

## Idempotency protocol (mandatory)

Snapshot before creating anything. If the application/connection already exists,
verify its configuration matches SUBMISSION.md, record what is missing, and only
create the missing pieces. On unexpected page state: stop, snapshot, report.

## Steps

1. Applications → create (or reuse) a Native application named after the app.
2. Settings: record the Domain and Client ID. Set the Allowed Callback URLs and
   Allowed Logout URLs to the app's redirect (the Expo scheme redirect, e.g.
   `apptemplate://redirect` for the template, or the scaffolded app's scheme).
   Cross-check the scheme against `scheme` in app.config.js.
3. Enable a Refresh Token grant (the template requests `offline_access`).
4. Authentication → Social → add and enable the Apple connection, then enable it
   for this application. This is REQUIRED: the template ships native Sign in with
   Apple, and Apple rejects apps offering other social logins without it. Ask the
   user for the Apple Services ID / key if Auth0 prompts — pause and never handle
   secrets in the repo.
5. Write the public values to `.env.production` and `.env.local`:
   `EXPO_PUBLIC_AUTH0_DOMAIN=<domain>` and `EXPO_PUBLIC_AUTH0_CLIENT_ID=<clientId>`.
   These files are gitignored — confirm with `git check-ignore .env.production`
   before writing. Never write the Auth0 client secret into the repo (the native
   PKCE flow does not need it).
6. Update SUBMISSION.md: record the domain, client ID, and callback URL; check off
   the Stage 1 auth items.
7. Run `npm run doctor` — the auth checks should pass.
```

- [ ] **Step 2: Commit**

```powershell
git add expo
git commit -m "Add auth-setup Auth0 skill"
```

---

### Task 12: Validation script assertion + CI

**Files:**
- Modify: `expo/templates/app/template-tests/... ` — NOTE: the validation script lives at `expo/template-tests/scaffold-and-validate.ps1` (repo-level, not inside the template). Modify that file.

- [ ] **Step 1: Add a partial-auth-config assertion to `expo/template-tests/scaffold-and-validate.ps1`**

Insert this block inside the `try { ... }`, immediately after the existing "Doctor must fail on a fresh scaffold" block and before the closing `}` of the `try`:
```powershell
    Write-Host "Partial auth config must fail the production config load..."
    $env:NODE_ENV = 'production'
    $env:EXPO_PUBLIC_REVENUECAT_IOS_API_KEY = 'smoke-key'
    $env:EXPO_PUBLIC_AUTH0_DOMAIN = 'smoke.auth0.com'
    node -e "try { require('./app.config.js'); process.exit(0) } catch (e) { process.exit(7) }"
    $partialExit = $LASTEXITCODE
    Remove-Item Env:NODE_ENV
    Remove-Item Env:EXPO_PUBLIC_REVENUECAT_IOS_API_KEY
    Remove-Item Env:EXPO_PUBLIC_AUTH0_DOMAIN
    if ($partialExit -ne 7) { throw "partial auth config did not fail app.config.js (exit $partialExit)" }
```

- [ ] **Step 2: Run the validation script locally**

Run (from repo root): `.\expo\template-tests\scaffold-and-validate.ps1`
Expected: ends with `Expo template validation passed.` then `Done.`. This scaffolds a fresh app (which now carries the auth layer), runs the full guardrail set against it, and exercises the new partial-auth assertion. It takes several minutes.

- [ ] **Step 3: Commit, push, watch CI**

```powershell
git add expo
git commit -m "Assert partial auth config fails in template validation"
git push
gh run list --limit 1
```
Then `gh run watch <id> --exit-status`. Report the conclusion of all jobs: `scaffold-and-test (cli)`, `scaffold-and-test (etl-api)`, `expo-scaffold-and-validate`. All must be green. If the expo job fails on `expo-auth-session` resolution under the runner's pwsh/Node, diagnose (version mismatch from `expo install`, native module in a JS-only validation) and report — the validation never builds native, only runs jest/tsc/eslint/node, so the auth modules must be import-safe under jest mocks (they are) and tree-shake cleanly under tsc.

---

## Self-review notes (already applied)

- **Spec coverage:** `src/lib/auth/` config-gated layer (T2–T6); SignInScreen + AuthGate + App wiring (T7); auto-link to RevenueCat (T6 + T7 effect); conditional env + Apple plugin (T8); doctor checks (T9); SUBMISSION.md + CLAUDE.md (T10); auth-setup skill (T11); validation + CI (T12). Guardrails: every `src/lib/auth` file has a colocated test and is coverage-gated (T2–T6 each run coverage; T5 step 4 enforces ≥80%).
- **Type consistency:** `TokenSet` defined once in `tokenStore.ts`, imported by `authClient.ts` and `useAuth.ts`. `AuthUser` defined once in `authClient.ts`, imported by `useAuth.ts` and `linkPurchases.ts`. `AuthConfig` in `authConfig.ts`. `AuthState` in `useAuth.ts`, imported by `AuthGate.tsx` and `SignInScreen.tsx`. `EXPO_PUBLIC_AUTH0_DOMAIN`/`EXPO_PUBLIC_AUTH0_CLIENT_ID` consistent across authConfig, app.config.js, doctor, .env.example, SUBMISSION.md, CLAUDE.md, auth-setup skill.
- **Deliberate deviation from the spec (flagged):** native Apple Sign In produces its own `TokenSet`/`AuthUser` rather than being exchanged into a single Auth0 identity. True single-identity unification needs a server-side token exchange, which the spec defers to per-app backend work. `getToken()` returns the active session's token regardless of provider. If you want strict single-identity-through-Auth0 in v1, that's a backend addition and should be its own spec.
- **Known risk, accepted:** the exact `expo-auth-session` call signatures (`AuthRequest` options, `exchangeCodeAsync`/`refreshAsync` config shapes) may differ slightly by installed version; `npx tsc --noEmit` in T7/T8 is the reconciliation gate. Do not paper over with `any`.
```
