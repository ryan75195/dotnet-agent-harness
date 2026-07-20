import { Platform } from 'react-native';
import Purchases from 'react-native-purchases';

import { initPurchases } from '../initPurchases';

function setPlatform(os: 'android' | 'ios') {
  Object.defineProperty(Platform, 'OS', { configurable: true, value: os });
}

describe('initPurchases', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    delete process.env.EXPO_PUBLIC_REVENUECAT_IOS_API_KEY;
    delete process.env.EXPO_PUBLIC_REVENUECAT_ANDROID_API_KEY;
    setPlatform('ios');
  });

  test('configures RevenueCat with the Apple TV key', () => {
    process.env.EXPO_PUBLIC_REVENUECAT_IOS_API_KEY = 'rc-apple-key';
    initPurchases();
    expect(Purchases.configure).toHaveBeenCalledWith({ apiKey: 'rc-apple-key' });
  });

  test('configures RevenueCat with the Android TV key', () => {
    setPlatform('android');
    process.env.EXPO_PUBLIC_REVENUECAT_ANDROID_API_KEY = 'rc-android-key';
    initPurchases();
    expect(Purchases.configure).toHaveBeenCalledWith({ apiKey: 'rc-android-key' });
  });

  test('does nothing when the active platform has no api key', () => {
    initPurchases();
    expect(Purchases.configure).not.toHaveBeenCalled();
  });
});
