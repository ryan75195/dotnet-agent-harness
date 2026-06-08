import Purchases from 'react-native-purchases';

import { initPurchases } from '../initPurchases';

describe('initPurchases', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    delete process.env.EXPO_PUBLIC_REVENUECAT_IOS_API_KEY;
  });

  test('configures RevenueCat when the api key is set', () => {
    process.env.EXPO_PUBLIC_REVENUECAT_IOS_API_KEY = 'rc-test-key';
    initPurchases();
    expect(Purchases.configure).toHaveBeenCalledWith({ apiKey: 'rc-test-key' });
  });

  test('does nothing when the api key is missing', () => {
    initPurchases();
    expect(Purchases.configure).not.toHaveBeenCalled();
  });
});
