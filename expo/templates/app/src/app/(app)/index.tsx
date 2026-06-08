import { useRouter } from 'expo-router';

import { HomeScreen } from '../../features/home/HomeScreen';
import { useAuthContext } from '../../lib/auth/AuthProvider';
import { useSubscription } from '../../lib/purchases/useSubscription';

export default function Home() {
  const router = useRouter();
  const auth = useAuthContext();
  const { isSubscribed } = useSubscription();
  return (
    <HomeScreen
      isSubscribed={isSubscribed}
      onUpgradePress={() => router.push('/paywall')}
      onSettingsPress={auth.isAuthenticated ? () => router.push('/settings') : undefined}
    />
  );
}
