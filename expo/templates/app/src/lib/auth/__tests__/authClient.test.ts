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

  test('returns null when success result has no code param', async () => {
    const original = AuthSession.AuthRequest;
    (AuthSession as { AuthRequest: unknown }).AuthRequest = class {
      codeVerifier = 'v';
      async promptAsync() {
        return { type: 'success', params: {} };
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

  test('nulls optional fields and zeros expiry when absent from response', async () => {
    (AuthSession.refreshAsync as jest.Mock).mockResolvedValueOnce({
      accessToken: 'access-3'
    });
    const tokens = await refreshTokens(config, 'refresh-1', 0);
    expect(tokens).toEqual({
      accessToken: 'access-3',
      refreshToken: null,
      idToken: null,
      expiresAt: 0
    });
  });
});

describe('loginWithAuth0 optional fields', () => {
  test('nulls optional fields when token response omits them', async () => {
    (AuthSession.exchangeCodeAsync as jest.Mock).mockResolvedValueOnce({
      accessToken: 'access-x'
    });
    const tokens = await loginWithAuth0(config, 0);
    expect(tokens).toEqual({
      accessToken: 'access-x',
      refreshToken: null,
      idToken: null,
      expiresAt: 0
    });
  });

  test('falls back to empty string when codeVerifier is absent', async () => {
    const original = AuthSession.AuthRequest;
    (AuthSession as { AuthRequest: unknown }).AuthRequest = class {
      async promptAsync() {
        return { type: 'success', params: { code: 'code-2' } };
      }
    };
    const tokens = await loginWithAuth0(config, 0);
    expect(tokens).not.toBeNull();
    (AuthSession as { AuthRequest: unknown }).AuthRequest = original;
  });
});
