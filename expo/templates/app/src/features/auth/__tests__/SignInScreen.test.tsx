import { fireEvent, render, screen } from '@testing-library/react-native';

import { SignInScreen } from '../SignInScreen';
import type { AuthState } from '../../../lib/auth/useAuth';

function authState(overrides: Partial<AuthState>): AuthState {
  return {
    isAuthEnabled: true,
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

describe('SignInScreen', () => {
  test('shows a spinner and no Continue while loading', () => {
    render(<SignInScreen auth={authState({ isLoading: true })} />);
    expect(screen.queryByText('Continue')).toBeNull();
  });

  test('triggers signIn when Continue is pressed', () => {
    const signIn = jest.fn();
    render(<SignInScreen auth={authState({ isLoading: false, signIn })} />);
    fireEvent.press(screen.getByText('Continue'));
    expect(signIn).toHaveBeenCalled();
  });
});
