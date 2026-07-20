import { Platform } from 'react-native';
import Purchases from 'react-native-purchases';

function getApiKey(): string {
  if (Platform.OS === 'ios') {
    return process.env.EXPO_PUBLIC_REVENUECAT_IOS_API_KEY ?? '';
  }
  if (Platform.OS === 'android') {
    return process.env.EXPO_PUBLIC_REVENUECAT_ANDROID_API_KEY ?? '';
  }
  return '';
}

export function initPurchases(): void {
  const apiKey = getApiKey();
  if (apiKey !== '') {
    Purchases.configure({ apiKey });
  }
}
