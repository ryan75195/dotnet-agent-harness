import * as AppleAuthentication from 'expo-apple-authentication';

import * as authClient from '../authClient';
import { appleSignIn, passwordSignIn, requestAccountDeletion, resolveToken, restoreSession } from '../authActions';
import * as tokenStore from '../tokenStore';

jest.mock('../authClient');
jest.mock('../tokenStore');

const mockedClient = authClient as jest.Mocked<typeof authClient>;
const mockedStore = tokenStore as jest.Mocked<typeof tokenStore>;
const mockedApple = AppleAuthentication as jest.Mocked<typeof AppleAuthentication>;

const config = { domain: 'tenant.auth0.com', clientId: 'client-123' };
const tokens = { accessToken: 'a', refreshToken: 'r', idToken: 'i', expiresAt: 9_999_999_999_999 };
const ada = { id: 'auth0|1', email: 'a@b.com', name: 'Ada' };

beforeEach(() => {
  jest.clearAllMocks();
  mockedClient.isExpired.mockReturnValue(false);
});

describe('restoreSession', () => {
  test('returns null when nothing is stored', async () => {
    mockedStore.loadTokens.mockResolvedValue(null);
    expect(await restoreSession(config)).toBeNull();
  });

  test('returns the session for a valid stored token', async () => {
    mockedStore.loadTokens.mockResolvedValue(tokens);
    mockedClient.fetchUserInfo.mockResolvedValue(ada);
    expect(await restoreSession(config)).toEqual({ tokens, user: ada });
  });

  test('clears and returns null when expired with no refresh token', async () => {
    mockedStore.loadTokens.mockResolvedValue({ ...tokens, refreshToken: null });
    mockedClient.isExpired.mockReturnValue(true);
    expect(await restoreSession(config)).toBeNull();
    expect(mockedStore.clearTokens).toHaveBeenCalled();
  });

  test('refreshes when expired with a refresh token', async () => {
    mockedStore.loadTokens.mockResolvedValue({ ...tokens, expiresAt: 1 });
    mockedClient.isExpired.mockReturnValue(true);
    mockedClient.refreshTokens.mockResolvedValue(tokens);
    mockedClient.fetchUserInfo.mockResolvedValue(ada);
    expect(await restoreSession(config)).toEqual({ tokens, user: ada });
    expect(mockedClient.refreshTokens).toHaveBeenCalled();
  });
});

describe('passwordSignIn', () => {
  test('returns null when login is cancelled', async () => {
    mockedClient.loginWithAuth0.mockResolvedValue(null);
    expect(await passwordSignIn(config)).toBeNull();
  });

  test('returns the session on success', async () => {
    mockedClient.loginWithAuth0.mockResolvedValue(tokens);
    mockedClient.fetchUserInfo.mockResolvedValue(ada);
    expect(await passwordSignIn(config)).toEqual({ tokens, user: ada });
    expect(mockedStore.saveTokens).toHaveBeenCalledWith(tokens);
  });
});

describe('appleSignIn', () => {
  test('returns null when cancelled', async () => {
    mockedApple.signInAsync.mockRejectedValue(new Error('cancelled'));
    expect(await appleSignIn()).toBeNull();
  });

  test('maps the credential to a session', async () => {
    mockedApple.signInAsync.mockResolvedValue({
      user: 'apple-9',
      email: 'a@icloud.com',
      fullName: { givenName: 'Ada', familyName: 'Lovelace' },
      identityToken: 'apple-id-token'
    } as Awaited<ReturnType<typeof AppleAuthentication.signInAsync>>);
    const session = await appleSignIn();
    expect(session?.user).toEqual({ id: 'apple-9', email: 'a@icloud.com', name: 'Ada Lovelace' });
    expect(session?.tokens.accessToken).toBe('apple-id-token');
  });

  test('returns null when Apple omits the identity token', async () => {
    mockedApple.signInAsync.mockResolvedValue({
      user: 'apple-9',
      email: null,
      fullName: null,
      identityToken: null
    } as Awaited<ReturnType<typeof AppleAuthentication.signInAsync>>);
    expect(await appleSignIn()).toBeNull();
  });

  test('null name when fullName is absent', async () => {
    mockedApple.signInAsync.mockResolvedValue({
      user: 'apple-9',
      email: null,
      fullName: null,
      identityToken: 't'
    } as Awaited<ReturnType<typeof AppleAuthentication.signInAsync>>);
    const session = await appleSignIn();
    expect(session?.user.name).toBeNull();
  });
});

describe('resolveToken', () => {
  test('returns the current token when valid', async () => {
    expect(await resolveToken(config, tokens)).toEqual({ token: 'a', tokens });
  });

  test('returns null when expired with no refresh token', async () => {
    mockedClient.isExpired.mockReturnValue(true);
    expect(await resolveToken(config, { ...tokens, refreshToken: null })).toBeNull();
  });

  test('refreshes when expired with a refresh token', async () => {
    mockedClient.isExpired.mockReturnValue(true);
    mockedClient.refreshTokens.mockResolvedValue({ ...tokens, accessToken: 'fresh' });
    const resolved = await resolveToken(config, tokens);
    expect(resolved?.token).toBe('fresh');
  });
});

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
