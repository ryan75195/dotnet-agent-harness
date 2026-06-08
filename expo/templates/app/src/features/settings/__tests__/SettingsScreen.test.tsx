import { fireEvent, render, screen } from '@testing-library/react-native';

import { SettingsScreen } from '../SettingsScreen';

function props(overrides: Partial<Parameters<typeof SettingsScreen>[0]> = {}) {
  return {
    isAuthenticated: true,
    canDeleteAccount: true,
    onSignOut: jest.fn(),
    onDeleteAccount: jest.fn(),
    ...overrides
  };
}

describe('SettingsScreen', () => {
  test('sign out calls onSignOut', () => {
    const onSignOut = jest.fn();
    render(<SettingsScreen {...props({ onSignOut })} />);
    fireEvent.press(screen.getByText('Sign out'));
    expect(onSignOut).toHaveBeenCalled();
  });

  test('hides sign out when not authenticated', () => {
    render(<SettingsScreen {...props({ isAuthenticated: false })} />);
    expect(screen.queryByText('Sign out')).toBeNull();
  });

  test('hides the delete section when canDeleteAccount is false', () => {
    render(<SettingsScreen {...props({ canDeleteAccount: false })} />);
    expect(screen.queryByText('Delete account')).toBeNull();
  });

  test('confirm flow: reveal, cancel, and confirm', () => {
    const onDeleteAccount = jest.fn();
    render(<SettingsScreen {...props({ onDeleteAccount })} />);
    fireEvent.press(screen.getByText('Delete account'));
    expect(screen.getByText('This permanently deletes your account.')).toBeTruthy();
    fireEvent.press(screen.getByText('Cancel'));
    expect(screen.queryByText('This permanently deletes your account.')).toBeNull();
    fireEvent.press(screen.getByText('Delete account'));
    fireEvent.press(screen.getByText('Confirm delete'));
    expect(onDeleteAccount).toHaveBeenCalled();
  });
});
