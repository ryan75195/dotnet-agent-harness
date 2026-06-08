import Purchases from 'react-native-purchases';

import { AuthUser } from './authClient';

export async function syncPurchasesIdentity(user: AuthUser | null): Promise<void> {
  const action = user ? Purchases.logIn(user.id) : Purchases.logOut();
  await action.then(
    () => undefined,
    () => undefined
  );
}
