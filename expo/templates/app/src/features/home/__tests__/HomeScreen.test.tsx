import { fireEvent, render, screen } from '@testing-library/react-native';

import { HomeScreen } from '../HomeScreen';

describe('HomeScreen', () => {
  test('shows the upgrade CTA and free plan when not subscribed', () => {
    render(<HomeScreen isSubscribed={false} onUpgradePress={jest.fn()} />);
    expect(screen.getByText('Upgrade')).toBeTruthy();
    expect(screen.getByText('Free plan')).toBeTruthy();
  });

  test('hides the CTA and shows premium when subscribed', () => {
    render(<HomeScreen isSubscribed={true} onUpgradePress={jest.fn()} />);
    expect(screen.queryByText('Upgrade')).toBeNull();
    expect(screen.getByText('Premium active')).toBeTruthy();
  });

  test('calls onUpgradePress when the CTA is pressed', () => {
    const onUpgradePress = jest.fn();
    render(<HomeScreen isSubscribed={false} onUpgradePress={onUpgradePress} />);
    fireEvent.press(screen.getByText('Upgrade'));
    expect(onUpgradePress).toHaveBeenCalled();
  });
});
