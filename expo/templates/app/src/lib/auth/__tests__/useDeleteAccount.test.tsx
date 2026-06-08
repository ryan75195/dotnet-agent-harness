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
