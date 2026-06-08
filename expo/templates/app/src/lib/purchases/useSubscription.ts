import { useEffect, useState } from 'react';
import Purchases, { CustomerInfo } from 'react-native-purchases';

export const PREMIUM_ENTITLEMENT = 'premium';

export type SubscriptionState = {
  isSubscribed: boolean;
  isLoading: boolean;
};

function hasPremium(info: CustomerInfo): boolean {
  return info.entitlements.active[PREMIUM_ENTITLEMENT] !== undefined;
}

export function useSubscription(): SubscriptionState {
  const [state, setState] = useState<SubscriptionState>({ isSubscribed: false, isLoading: true });

  useEffect(() => {
    let mounted = true;
    Purchases.getCustomerInfo()
      .then((info) => {
        if (mounted) {
          setState({ isSubscribed: hasPremium(info), isLoading: false });
        }
      })
      .catch(() => {
        if (mounted) {
          setState({ isSubscribed: false, isLoading: false });
        }
      });
    Purchases.addCustomerInfoUpdateListener((info) => {
      if (mounted) {
        setState({ isSubscribed: hasPremium(info), isLoading: false });
      }
    });
    return () => {
      mounted = false;
    };
  }, []);

  return state;
}
