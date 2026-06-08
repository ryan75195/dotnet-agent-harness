import { Stack } from 'expo-router';

import { AuthProvider } from '../lib/auth/AuthProvider';
import { initPurchases } from '../lib/purchases/initPurchases';

initPurchases();

export default function RootLayout() {
  return (
    <AuthProvider>
      <Stack screenOptions={{ headerShown: false }}>
        <Stack.Screen name="(app)" />
        <Stack.Screen name="sign-in" />
      </Stack>
    </AuthProvider>
  );
}
