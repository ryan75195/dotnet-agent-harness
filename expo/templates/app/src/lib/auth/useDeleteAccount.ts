import { useCallback } from 'react';

import { useAuthContext } from './AuthProvider';
import { requestAccountDeletion } from './authActions';
import { getAccountDeleteUrl } from './authConfig';

export type DeleteAccountState = {
  canDeleteAccount: boolean;
  deleteAccount: () => Promise<boolean>;
};

export function useDeleteAccount(): DeleteAccountState {
  const auth = useAuthContext();
  const deleteUrl = getAccountDeleteUrl();

  const deleteAccount = useCallback(async () => {
    if (deleteUrl === null) {
      return false;
    }
    const token = await auth.getToken();
    if (token === null) {
      return false;
    }
    const ok = await requestAccountDeletion(deleteUrl, token);
    if (ok) {
      await auth.signOut();
    }
    return ok;
  }, [auth, deleteUrl]);

  return {
    canDeleteAccount: deleteUrl !== null && auth.isAuthenticated,
    deleteAccount
  };
}
