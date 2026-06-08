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
