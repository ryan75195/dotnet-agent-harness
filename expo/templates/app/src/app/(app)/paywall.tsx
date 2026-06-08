import { useRouter } from 'expo-router';

import { PaywallScreen } from '../../features/paywall/PaywallScreen';

export default function Paywall() {
  const router = useRouter();
  return <PaywallScreen onClose={() => router.back()} />;
}
