import { fireEvent, render, screen } from '@testing-library/react-native';

import { HomeScreen } from '../HomeScreen';

function props(overrides: Partial<Parameters<typeof HomeScreen>[0]> = {}) {
  return {
    isSubscribed: false,
    onBrowsePress: jest.fn(),
    onUpgradePress: jest.fn(),
    ...overrides
  };
}

describe('HomeScreen', () => {
  test('shows the library action and free-plan upgrade CTA', () => {
    render(<HomeScreen {...props()} />);
    expect(screen.getByText('Browse library')).toBeTruthy();
    expect(screen.getByText('Upgrade')).toBeTruthy();
    expect(screen.getByText('Free plan')).toBeTruthy();
  });

  test('hides the upgrade CTA for a subscriber', () => {
    render(<HomeScreen {...props({ isSubscribed: true })} />);
    expect(screen.queryByText('Upgrade')).toBeNull();
    expect(screen.getByText('Premium active')).toBeTruthy();
  });

  test('calls the provided navigation handlers', () => {
    const onBrowsePress = jest.fn();
    const onSettingsPress = jest.fn();
    render(<HomeScreen {...props({ onBrowsePress, onSettingsPress })} />);
    fireEvent.press(screen.getByText('Browse library'));
    fireEvent.press(screen.getByText('Settings'));
    expect(onBrowsePress).toHaveBeenCalled();
    expect(onSettingsPress).toHaveBeenCalled();
  });
});
