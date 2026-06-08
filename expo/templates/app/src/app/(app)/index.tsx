import { useRouter } from 'expo-router';

import { HomeScreen } from '../../features/home/HomeScreen';
import { useSubscription } from '../../lib/purchases/useSubscription';

export default function Home() {
  const router = useRouter();
  const { isSubscribed } = useSubscription();
  return <HomeScreen isSubscribed={isSubscribed} onUpgradePress={() => router.push('/paywall')} />;
}
