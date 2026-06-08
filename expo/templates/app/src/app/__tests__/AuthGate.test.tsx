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
