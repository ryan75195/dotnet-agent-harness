import { useState } from 'react';
import { StatusBar } from 'expo-status-bar';

import { HomeScreen } from './src/app/HomeScreen';
import { PaywallScreen } from './src/app/PaywallScreen';
import { initPurchases } from './src/lib/purchases/initPurchases';
import { useSubscription } from './src/lib/purchases/useSubscription';

initPurchases();

export default function App() {
  const [showPaywall, setShowPaywall] = useState(false);
  const { isSubscribed } = useSubscription();

  if (showPaywall && !isSubscribed) {
    return <PaywallScreen onClose={() => setShowPaywall(false)} />;
  }

  return (
    <>
      <HomeScreen isSubscribed={isSubscribed} onUpgradePress={() => setShowPaywall(true)} />
      <StatusBar style="light" />
    </>
  );
}
