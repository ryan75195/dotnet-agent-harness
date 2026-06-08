import { render, screen, waitFor } from '@testing-library/react-native';
import Purchases from 'react-native-purchases';

import { PaywallScreen } from '../PaywallScreen';

const mockedPurchases = Purchases as jest.Mocked<typeof Purchases>;

type Offerings = Awaited<ReturnType<typeof Purchases.getOfferings>>;

function offeringsWith(packages: Array<{ identifier: string; title: string; priceString: string }>): Offerings {
  return {
    current: {
      availablePackages: packages.map((pkg) => ({
        identifier: pkg.identifier,
        product: { title: pkg.title, priceString: pkg.priceString }
      }))
    }
  } as unknown as Offerings;
}

describe('PaywallScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  test('renders packages from the current offering', async () => {
    mockedPurchases.getOfferings.mockResolvedValue(
      offeringsWith([{ identifier: 'monthly', title: 'Plus Monthly', priceString: '£4.99' }])
    );
    render(<PaywallScreen onClose={jest.fn()} />);
    await waitFor(() => expect(screen.getByText('Plus Monthly')).toBeTruthy());
    expect(screen.getByText('£4.99')).toBeTruthy();
  });

  test('renders the close action when offerings fail to load', async () => {
    mockedPurchases.getOfferings.mockRejectedValue(new Error('network'));
    render(<PaywallScreen onClose={jest.fn()} />);
    await waitFor(() => expect(screen.getByText('Not now')).toBeTruthy());
  });

  test('renders empty package list when current offering is null', async () => {
    mockedPurchases.getOfferings.mockResolvedValue({ current: null } as unknown as Offerings);
    render(<PaywallScreen onClose={jest.fn()} />);
    await waitFor(() => expect(screen.getByText('Go Premium')).toBeTruthy());
    expect(screen.queryByRole('button', { name: /monthly/i })).toBeNull();
  });
});
