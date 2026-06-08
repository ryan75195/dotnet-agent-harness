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
