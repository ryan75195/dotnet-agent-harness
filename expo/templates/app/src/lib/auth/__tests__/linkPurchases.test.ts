import Purchases from 'react-native-purchases';

import { syncPurchasesIdentity } from '../linkPurchases';

const mockedPurchases = Purchases as jest.Mocked<typeof Purchases>;

describe('syncPurchasesIdentity', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  test('logs in RevenueCat with the user id when a user is present', async () => {
    await syncPurchasesIdentity({ id: 'auth0|1', email: null, name: null });
    expect(mockedPurchases.logIn).toHaveBeenCalledWith('auth0|1');
    expect(mockedPurchases.logOut).not.toHaveBeenCalled();
  });

  test('logs out RevenueCat when the user is null', async () => {
    await syncPurchasesIdentity(null);
    expect(mockedPurchases.logOut).toHaveBeenCalled();
    expect(mockedPurchases.logIn).not.toHaveBeenCalled();
  });

  test('does not throw when RevenueCat rejects', async () => {
    mockedPurchases.logIn.mockRejectedValueOnce(new Error('not configured'));
    await expect(syncPurchasesIdentity({ id: 'auth0|1', email: null, name: null })).resolves.toBeUndefined();
  });
});
