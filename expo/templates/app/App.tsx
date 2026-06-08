import { useEffect, useState } from 'react';
import { StatusBar } from 'expo-status-bar';

import { AuthGate } from './src/app/AuthGate';
import { HomeScreen } from './src/app/HomeScreen';
import { PaywallScreen } from './src/app/PaywallScreen';
import { syncPurchasesIdentity } from './src/lib/auth/linkPurchases';
import { useAuth } from './src/lib/auth/useAuth';
import { initPurchases } from './src/lib/purchases/initPurchases';
import { useSubscription } from './src/lib/purchases/useSubscription';

initPurchases();

export default function App() {
  const [showPaywall, setShowPaywall] = useState(false);
  const { isSubscribed } = useSubscription();
  const auth = useAuth();

  useEffect(() => {
    syncPurchasesIdentity(auth.user);
  }, [auth.user]);

  return (
    <AuthGate auth={auth}>
      {showPaywall && !isSubscribed ? (
        <PaywallScreen onClose={() => setShowPaywall(false)} />
      ) : (
        <HomeScreen isSubscribed={isSubscribed} onUpgradePress={() => setShowPaywall(true)} />
      )}
      <StatusBar style="light" />
    </AuthGate>
  );
}
