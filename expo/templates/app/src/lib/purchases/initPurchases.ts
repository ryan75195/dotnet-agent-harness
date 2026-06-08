import { Platform } from 'react-native';
import Purchases from 'react-native-purchases';

export function initPurchases(): void {
  const apiKey = process.env.EXPO_PUBLIC_REVENUECAT_IOS_API_KEY ?? '';
  if (Platform.OS !== 'ios' || apiKey === '') {
    return;
  }
  Purchases.configure({ apiKey });
}
