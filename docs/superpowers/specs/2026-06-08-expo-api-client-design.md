# Expo Authenticated API Client — Design

**Date:** 2026-06-08
**Status:** Approved

## Goal

Add an authenticated API client to the Expo template (`src/lib/api/`): a typed
fetch wrapper that auto-attaches the auth bearer token from `getToken()`, reads
its base URL from env, and surfaces errors as a typed `ApiError`. It bridges the
auth layer (which produces a token) to actual backend calls — the client side of
talking to a backend. The template stays frontend-only; this is purely the
client.

## Scope decisions

| Decision | Choice |
|---|---|
| Surface | `createApiClient(getToken)` factory (React-agnostic) + `useApi()` hook. |
| Methods | `get`, `post`, `put`, `del` (typed). No patch/interceptors/query-builder. |
| Config gating | `EXPO_PUBLIC_API_BASE_URL`, optional. No doctor check, no app.config validation (a backend-less app must still build green). |
| Auth | Bearer attached only when `getToken()` returns a token; unauthenticated requests work (public endpoints, or auth-off apps). |
| Errors | Non-2xx throws `ApiError(status, body)`. 2xx parses JSON; 204/empty → undefined. |
| 401 retry | Out of scope v1 — `getToken()` already refreshes proactively near expiry. |

## 1. Components

All under `src/lib/api/`, coverage-gated (≥80%), each with a colocated test.

- **`ApiError.ts`** — `export class ApiError extends Error` with
  `status: number` and `body: unknown`. The single error the client throws on a
  non-2xx response. (One uppercase export — satisfies one-component-per-file.)
- **`apiClient.ts`** — the pure core, usable in or out of React:
  - `getApiBaseUrl(): string | null` — reads `EXPO_PUBLIC_API_BASE_URL`
    (empty/unset → null).
  - `type TokenProvider = () => Promise<string | null>`.
  - `type ApiClient = { get, post, put, del }` with signatures
    `get<T>(path: string): Promise<T>`,
    `post<T>(path: string, body?: unknown): Promise<T>`,
    `put<T>(path: string, body?: unknown): Promise<T>`,
    `del<T>(path: string): Promise<T>`.
  - `createApiClient(getToken: TokenProvider): ApiClient`. Each method:
    resolves the base URL (throws `ApiError` / a clear Error if null), calls
    `getToken()`, builds headers (`Content-Type: application/json` for bodied
    requests; `Authorization: Bearer <token>` only when token is non-null),
    `fetch`es `${baseUrl}${path}`, and: on non-2xx throws `ApiError(status,
    parsedBody)`; on 204/empty returns `undefined`; otherwise parses and returns
    JSON as `T`.
- **`useApi.ts`** — `useApi(): ApiClient` =
  `createApiClient(useAuthContext().getToken)`, memoized on the auth identity.
  The React-side convenience; a screen does
  `const api = useApi(); await api.get<Profile>('/me')`.

## 2. Config gating and boundaries

- `EXPO_PUBLIC_API_BASE_URL` is OPTIONAL. A backend-less app never sets it and is
  unaffected. When unset, calling a client method throws a clear error
  ("EXPO_PUBLIC_API_BASE_URL is not set") — apps that use the client are expected
  to set it.
- No `submission-doctor` check (a backend is not an Apple requirement) and no
  `app.config.js` production validation (a no-backend app must build green).
- `.env.example` gains `EXPO_PUBLIC_API_BASE_URL=`. README gains an "API client"
  usage section. CLAUDE.md's `src/lib/` bullet already lists "api" as an example;
  add one sentence pointing at `useApi`/`createApiClient`.

## 3. Layering

`src/lib/api` imports nothing from `app`/`features`/`components`. `useApi`
imports `useAuthContext` from `src/lib/auth` — lib-to-lib, permitted by the
existing dependency-cruiser rules (the only lib rule forbids lib importing
up into app/features/components). No new rule needed.

## 4. Testing

- `ApiError`: constructs with `status` + `body`; is an `instanceof Error`.
- `apiClient` (mock global `fetch` + a fake `TokenProvider`):
  - joins `${baseUrl}${path}`.
  - attaches `Authorization: Bearer <token>` when the provider returns a token;
    omits it when the provider returns null.
  - `post`/`put` serialize the body as JSON with `Content-Type`.
  - parses and returns JSON on 200; returns `undefined` on 204.
  - non-2xx throws `ApiError` carrying the status and parsed body.
  - throws when `getApiBaseUrl()` is null (base URL unset).
- `useApi` (mock `useAuthContext`): returns a client whose `get` issues a fetch
  carrying the context token (proves the hook binds `auth.getToken`).
- All gated files ≥80% lines/branches.

## 5. Validation

`expo/template-tests/scaffold-and-validate.ps1` already runs the full guardrail
set on a fresh scaffold, which exercises the new lib + tests. No new assertion is
needed (the API client has no doctor/config gate to prove); CI green on the
scaffold is sufficient.

## Out of scope (v1)

- 401-retry-with-refresh (covered by `getToken()`'s proactive refresh).
- `patch`, request/response interceptors, query-param builders, retries,
  timeouts, caching (react-query is the app's choice on top of this).
- A backend or any server code (the template is frontend-only).
