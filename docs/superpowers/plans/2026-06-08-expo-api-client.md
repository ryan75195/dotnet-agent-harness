# Expo Authenticated API Client Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `src/lib/api/` — a typed fetch wrapper (`createApiClient` + `useApi`) that auto-attaches the auth bearer token, reads its base URL from env, and throws a typed `ApiError` on non-2xx.

**Architecture:** A pure `apiClient` (factory + `getApiBaseUrl`, usable outside React) plus a thin `useApi` hook binding `useAuthContext().getToken`. Errors surface as an `ApiError` class. All config-gated by `EXPO_PUBLIC_API_BASE_URL` (optional — no doctor/app.config gate).

**Tech Stack:** TypeScript, fetch, jest + @testing-library/react-native.

**Spec:** `docs/superpowers/specs/2026-06-08-expo-api-client-design.md`

**Working notes:**
- Work in `expo/templates/app/`. No pre-commit hooks; commit to `main`. NO COMMENTS / no `any` / no `eslint-disable`.
- `src/lib/api` is coverage-gated (≥80%) and each module needs a colocated `__tests__/<name>.test.ts(x)`.
- `apiClient.ts` is a `.ts` file (no JSX), so generic arrow methods `get: <T>(path: string) => ...` are valid syntax. Keep it `.ts`.
- `AuthState` (from `src/lib/auth/useAuth`) already exposes `getToken: () => Promise<string | null>` — that is the `TokenProvider`. lib-to-lib import (`api` → `auth`) is allowed by the existing dependency-cruiser rules; no new rule.

---

## File structure (additions)

```
src/lib/api/
  ApiError.ts (+__tests__)        error class with status + body
  apiClient.ts (+__tests__)       getApiBaseUrl + createApiClient (get/post/put/del)
  useApi.ts (+__tests__)          hook binding useAuthContext().getToken
.env.example                      + EXPO_PUBLIC_API_BASE_URL=
README.md                         + "API client" usage section
CLAUDE.md                         + one sentence on the src/lib bullet
```

---

### Task 1: ApiError + apiClient (TDD)

**Files:** Create `src/lib/api/ApiError.ts` (+test), `src/lib/api/apiClient.ts` (+test)

- [ ] **Step 1: Write the failing ApiError test**

`src/lib/api/__tests__/ApiError.test.ts`:
```ts
import { ApiError } from '../ApiError';

describe('ApiError', () => {
  test('carries status and body and is an Error', () => {
    const error = new ApiError(404, { message: 'nope' });
    expect(error).toBeInstanceOf(Error);
    expect(error.status).toBe(404);
    expect(error.body).toEqual({ message: 'nope' });
    expect(error.name).toBe('ApiError');
  });
});
```

- [ ] **Step 2: Run, verify FAIL**

Run: `npx jest src/lib/api/__tests__/ApiError`
Expected: FAIL — cannot find `../ApiError`.

- [ ] **Step 3: Implement `src/lib/api/ApiError.ts`**

```ts
export class ApiError extends Error {
  readonly status: number;
  readonly body: unknown;

  constructor(status: number, body: unknown) {
    super(`Request failed with status ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
  }
}
```

- [ ] **Step 4: Run, verify PASS**

Run: `npx jest src/lib/api/__tests__/ApiError` → PASS.

- [ ] **Step 5: Write the failing apiClient test**

`src/lib/api/__tests__/apiClient.test.ts`:
```ts
import { ApiError } from '../ApiError';
import { createApiClient, getApiBaseUrl } from '../apiClient';

function jsonResponse(status: number, data: unknown) {
  return {
    ok: status >= 200 && status < 300,
    status,
    text: async () => JSON.stringify(data)
  } as unknown as Response;
}

describe('getApiBaseUrl', () => {
  beforeEach(() => {
    delete process.env.EXPO_PUBLIC_API_BASE_URL;
  });

  test('null when unset', () => {
    expect(getApiBaseUrl()).toBeNull();
  });

  test('returns the url when set', () => {
    process.env.EXPO_PUBLIC_API_BASE_URL = 'https://api.example.com';
    expect(getApiBaseUrl()).toBe('https://api.example.com');
  });
});

describe('createApiClient', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    process.env.EXPO_PUBLIC_API_BASE_URL = 'https://api.example.com';
  });

  test('GET attaches the bearer token and returns parsed json', async () => {
    global.fetch = jest.fn().mockResolvedValue(jsonResponse(200, { id: 1 })) as unknown as typeof fetch;
    const client = createApiClient(async () => 'tok-1');
    const result = await client.get('/me');
    expect(result).toEqual({ id: 1 });
    expect(global.fetch).toHaveBeenCalledWith('https://api.example.com/me', {
      method: 'GET',
      headers: { Authorization: 'Bearer tok-1' },
      body: undefined
    });
  });

  test('omits the Authorization header when there is no token', async () => {
    global.fetch = jest.fn().mockResolvedValue(jsonResponse(200, {})) as unknown as typeof fetch;
    const client = createApiClient(async () => null);
    await client.get('/public');
    expect(global.fetch).toHaveBeenCalledWith('https://api.example.com/public', {
      method: 'GET',
      headers: {},
      body: undefined
    });
  });

  test('POST serializes the body with a json content type', async () => {
    global.fetch = jest.fn().mockResolvedValue(jsonResponse(201, { ok: true })) as unknown as typeof fetch;
    const client = createApiClient(async () => 'tok-1');
    await client.post('/items', { name: 'x' });
    expect(global.fetch).toHaveBeenCalledWith('https://api.example.com/items', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: 'Bearer tok-1' },
      body: JSON.stringify({ name: 'x' })
    });
  });

  test('PUT serializes the body and returns json', async () => {
    global.fetch = jest.fn().mockResolvedValue(jsonResponse(200, { updated: true })) as unknown as typeof fetch;
    const client = createApiClient(async () => 'tok-1');
    expect(await client.put('/items/1', { name: 'y' })).toEqual({ updated: true });
  });

  test('returns undefined for a 204 response', async () => {
    global.fetch = jest.fn().mockResolvedValue({ ok: true, status: 204, text: async () => '' } as unknown as Response) as unknown as typeof fetch;
    const client = createApiClient(async () => 'tok-1');
    expect(await client.del('/items/1')).toBeUndefined();
  });

  test('returns undefined for a 200 with an empty body', async () => {
    global.fetch = jest.fn().mockResolvedValue({ ok: true, status: 200, text: async () => '' } as unknown as Response) as unknown as typeof fetch;
    const client = createApiClient(async () => 'tok-1');
    expect(await client.get('/ping')).toBeUndefined();
  });

  test('throws ApiError with status and body on a non-2xx response', async () => {
    global.fetch = jest.fn().mockResolvedValue(jsonResponse(404, { message: 'nope' })) as unknown as typeof fetch;
    const client = createApiClient(async () => 'tok-1');
    await expect(client.get('/missing')).rejects.toBeInstanceOf(ApiError);
    await expect(client.get('/missing')).rejects.toMatchObject({ status: 404, body: { message: 'nope' } });
  });

  test('throws when the base url is unset', async () => {
    delete process.env.EXPO_PUBLIC_API_BASE_URL;
    const client = createApiClient(async () => 'tok-1');
    await expect(client.get('/me')).rejects.toThrow('EXPO_PUBLIC_API_BASE_URL is not set');
  });
});
```

- [ ] **Step 6: Run, verify FAIL**

Run: `npx jest src/lib/api/__tests__/apiClient`
Expected: FAIL — cannot find `../apiClient`.

- [ ] **Step 7: Implement `src/lib/api/apiClient.ts`**

```ts
import { ApiError } from './ApiError';

export type TokenProvider = () => Promise<string | null>;

export type ApiClient = {
  get: <T>(path: string) => Promise<T>;
  post: <T>(path: string, body?: unknown) => Promise<T>;
  put: <T>(path: string, body?: unknown) => Promise<T>;
  del: <T>(path: string) => Promise<T>;
};

export function getApiBaseUrl(): string | null {
  const url = process.env.EXPO_PUBLIC_API_BASE_URL ?? '';
  return url === '' ? null : url;
}

async function parseBody(response: Response): Promise<unknown> {
  if (response.status === 204) {
    return undefined;
  }
  const text = await response.text();
  return text === '' ? undefined : JSON.parse(text);
}

async function request<T>(getToken: TokenProvider, method: string, path: string, body?: unknown): Promise<T> {
  const baseUrl = getApiBaseUrl();
  if (baseUrl === null) {
    throw new Error('EXPO_PUBLIC_API_BASE_URL is not set');
  }
  const token = await getToken();
  const headers: Record<string, string> = {};
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }
  if (token !== null) {
    headers.Authorization = `Bearer ${token}`;
  }
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  const parsed = await parseBody(response);
  if (!response.ok) {
    throw new ApiError(response.status, parsed);
  }
  return parsed as T;
}

export function createApiClient(getToken: TokenProvider): ApiClient {
  return {
    get: <T>(path: string) => request<T>(getToken, 'GET', path),
    post: <T>(path: string, body?: unknown) => request<T>(getToken, 'POST', path, body),
    put: <T>(path: string, body?: unknown) => request<T>(getToken, 'PUT', path, body),
    del: <T>(path: string) => request<T>(getToken, 'DELETE', path)
  };
}
```

- [ ] **Step 8: Run, verify PASS + coverage**

Run: `npx jest src/lib/api --coverage --collectCoverageFrom="src/lib/api/**/*.ts"`
Expected: ApiError + apiClient tests pass; both files ≥80% lines/branches (the parseBody 204/empty/json branches, the token null/present and body undefined/present branches, ok/not-ok, and base-url-null are all exercised).

- [ ] **Step 9: Commit**

```powershell
git add expo
git commit -m "Add ApiError and authenticated apiClient factory"
```

---

### Task 2: useApi hook (TDD)

**Files:** Create `src/lib/api/useApi.ts` (+test)

- [ ] **Step 1: Write the failing test**

`src/lib/api/__tests__/useApi.test.tsx`:
```tsx
import { renderHook } from '@testing-library/react-native';

import * as AuthProvider from '../../auth/AuthProvider';
import type { AuthState } from '../../auth/useAuth';
import { useApi } from '../useApi';

jest.mock('../../auth/AuthProvider');

const mockedContext = AuthProvider as jest.Mocked<typeof AuthProvider>;

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

describe('useApi', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    process.env.EXPO_PUBLIC_API_BASE_URL = 'https://api.example.com';
  });

  test('returns a client that issues requests with the context token', async () => {
    global.fetch = jest.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: async () => JSON.stringify({ id: 1 })
    } as unknown as Response) as unknown as typeof fetch;
    mockedContext.useAuthContext.mockReturnValue(authState({}));
    const { result } = renderHook(() => useApi());
    const data = await result.current.get('/me');
    expect(data).toEqual({ id: 1 });
    expect(global.fetch).toHaveBeenCalledWith(
      'https://api.example.com/me',
      expect.objectContaining({ headers: { Authorization: 'Bearer tok-1' } })
    );
  });
});
```

- [ ] **Step 2: Run, verify FAIL**

Run: `npx jest src/lib/api/__tests__/useApi`
Expected: FAIL — cannot find `../useApi`.

- [ ] **Step 3: Implement `src/lib/api/useApi.ts`**

```ts
import { useMemo } from 'react';

import { useAuthContext } from '../auth/AuthProvider';
import { ApiClient, createApiClient } from './apiClient';

export function useApi(): ApiClient {
  const auth = useAuthContext();
  return useMemo(() => createApiClient(auth.getToken), [auth]);
}
```

- [ ] **Step 4: Run, verify PASS + coverage**

Run: `npx jest src/lib/api/__tests__/useApi --coverage --collectCoverageFrom="src/lib/api/useApi.ts"`
Expected: PASS; useApi.ts ≥80%.

- [ ] **Step 5: Full verify + commit**

Run: `npm run verify`
Expected: all gates pass. depcruise: `src/lib/api` → `src/lib/auth` is lib-to-lib (allowed); no upward import. Commit:
```powershell
git add expo
git commit -m "Add useApi hook binding the auth token provider"
```

---

### Task 3: Docs + CI

**Files:** Modify `.env.example`, `README.md`, `CLAUDE.md`

- [ ] **Step 1: Append to `.env.example`**

```
EXPO_PUBLIC_API_BASE_URL=
```

- [ ] **Step 2: Add an "API client" section to `README.md`** (append at the end)

```markdown
## API client

`src/lib/api/` is a typed fetch wrapper that attaches the Auth0 bearer token
automatically. Set `EXPO_PUBLIC_API_BASE_URL` to your backend's base URL, then:

```tsx
import { useApi } from './src/lib/api/useApi';

function Profile() {
  const api = useApi();
  // await api.get<Profile>('/me')      -> GET  with Bearer token
  // await api.post<Item>('/items', it) -> POST with json body + Bearer token
}
```

Requests carry `Authorization: Bearer <token>` when a user is signed in and omit
it otherwise (public endpoints still work). Non-2xx responses throw an `ApiError`
with `status` and `body`. Outside React, use
`createApiClient(getToken)` directly. The base URL is optional — a backend-less
app simply never sets it.
```
Note: the inner triple-backtick `tsx` fence inside the markdown is a nested code
block and renders fine; ensure the outer section is plain markdown text.

- [ ] **Step 3: Update `CLAUDE.md`**

In the Architecture section, find the `src/lib/` bullet (it begins
"`src/lib/` — platform/service wrappers"). Append this sentence to that bullet:
```markdown
  The API client (`src/lib/api/`) wraps `fetch` and auto-attaches the auth bearer
  token; use `useApi()` in screens or `createApiClient(getToken)` elsewhere.
```

- [ ] **Step 4: Verify + commit + push + watch CI**

Run: `npm run verify` → green.
```powershell
git add expo
git commit -m "Document the API client in env, README, and CLAUDE.md"
git push
gh run list --limit 1
```
Then `gh run watch <id> --exit-status`. Report all three jobs (`scaffold-and-test (cli)`, `scaffold-and-test (etl-api)`, `expo-scaffold-and-validate`) — all must be green. The expo job scaffolds a fresh app including `src/lib/api` and runs the full guardrail set. On failure, `gh run view <id> --log-failed`, diagnose, fix.

---

## Self-review notes (already applied)

- **Spec coverage:** ApiError (T1); getApiBaseUrl + createApiClient with get/post/put/del, bearer auto-attach, ApiError on non-2xx, 204/empty handling, base-url-unset throw (T1); useApi hook (T2); .env + README + CLAUDE.md (T3). No doctor/app.config gate (per spec — backend optional). No new depcruise rule (lib-to-lib allowed).
- **Type consistency:** `TokenProvider = () => Promise<string | null>` matches `AuthState.getToken`. `ApiClient` shape identical across apiClient, useApi, and tests. `EXPO_PUBLIC_API_BASE_URL` identical across apiClient, .env.example, README, CLAUDE.md.
- **Coverage guard:** the parseBody empty-body branch has its own test (200 + empty) so branch coverage on apiClient stays ≥80%.
- **Placeholder scan:** none.
