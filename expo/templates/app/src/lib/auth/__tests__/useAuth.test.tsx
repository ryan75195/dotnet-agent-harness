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

  test('restore does not authenticate when expired token has no refresh token', async () => {
    enableAuth();
    mockedStore.loadTokens.mockResolvedValue({ ...validTokens, expiresAt: 1, refreshToken: null });
    mockedClient.isExpired.mockReturnValue(true);
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isAuthenticated).toBe(false);
    expect(mockedStore.clearTokens).toHaveBeenCalled();
  });

  test('restore clears tokens and remains unauthenticated when an error is thrown', async () => {
    enableAuth();
    mockedStore.loadTokens.mockRejectedValue(new Error('storage error'));
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isAuthenticated).toBe(false);
    expect(mockedStore.clearTokens).toHaveBeenCalled();
  });

  test('signIn leaves unauthenticated when loginWithAuth0 returns null', async () => {
    enableAuth();
    mockedClient.loginWithAuth0.mockResolvedValue(null);
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    await act(async () => {
      await result.current.signIn();
    });
    expect(result.current.isAuthenticated).toBe(false);
  });

  test('getToken returns null when authenticated but no refresh token available', async () => {
    enableAuth();
    mockedStore.loadTokens.mockResolvedValue({ ...validTokens, refreshToken: null });
    mockedClient.fetchUserInfo.mockResolvedValue(ada);
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isAuthenticated).toBe(true));
    mockedClient.isExpired.mockReturnValue(true);
    expect(await result.current.getToken()).toBeNull();
  });

  test('signInWithApple leaves unauthenticated when auth is disabled', async () => {
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    await act(async () => {
      await result.current.signInWithApple();
    });
    expect(result.current.isAuthenticated).toBe(false);
    expect(mockedApple.signInAsync).not.toHaveBeenCalled();
  });

  test('signInWithApple leaves unauthenticated when signInAsync throws', async () => {
    enableAuth();
    mockedApple.signInAsync.mockRejectedValue(new Error('user cancelled'));
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    await act(async () => {
      await result.current.signInWithApple();
    });
    expect(result.current.isAuthenticated).toBe(false);
  });

  test('signInWithApple sets null name when fullName is null', async () => {
    enableAuth();
    mockedApple.signInAsync.mockResolvedValue({
      user: 'apple-x',
      email: 'x@icloud.com',
      fullName: null,
      identityToken: 'tok'
    } as Awaited<ReturnType<typeof AppleAuthentication.signInAsync>>);
    const { result } = renderHook(() => useAuth());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    await act(async () => {
      await result.current.signInWithApple();
    });
    expect(result.current.isAuthenticated).toBe(true);
    expect(result.current.user?.name).toBeNull();
  });
});
