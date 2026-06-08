import { useRouter } from 'expo-router';

import { SettingsScreen } from '../../features/settings/SettingsScreen';
import { useAuthContext } from '../../lib/auth/AuthProvider';
import { useDeleteAccount } from '../../lib/auth/useDeleteAccount';

export default function Settings() {
  const router = useRouter();
  const auth = useAuthContext();
  const { canDeleteAccount, deleteAccount } = useDeleteAccount();

  const handleSignOut = async () => {
    await auth.signOut();
    router.replace('/');
  };

  const handleDelete = async () => {
    const ok = await deleteAccount();
    if (ok) {
      router.replace('/');
    }
  };

  return (
    <SettingsScreen
      isAuthenticated={auth.isAuthenticated}
      canDeleteAccount={canDeleteAccount}
      onSignOut={handleSignOut}
      onDeleteAccount={handleDelete}
    />
  );
}
