import { getAccountDeleteUrl, getAuthConfig, isAuthEnabled } from '../authConfig';

describe('authConfig', () => {
  beforeEach(() => {
    delete process.env.EXPO_PUBLIC_AUTH0_DOMAIN;
    delete process.env.EXPO_PUBLIC_AUTH0_CLIENT_ID;
  });

  test('isAuthEnabled is false when both vars are unset', () => {
    expect(isAuthEnabled()).toBe(false);
    expect(getAuthConfig()).toBeNull();
  });

  test('isAuthEnabled is false when only one var is set', () => {
    process.env.EXPO_PUBLIC_AUTH0_DOMAIN = 'tenant.auth0.com';
    expect(isAuthEnabled()).toBe(false);
    expect(getAuthConfig()).toBeNull();
  });

  test('returns config when both vars are set', () => {
    process.env.EXPO_PUBLIC_AUTH0_DOMAIN = 'tenant.auth0.com';
    process.env.EXPO_PUBLIC_AUTH0_CLIENT_ID = 'client-123';
    expect(isAuthEnabled()).toBe(true);
    expect(getAuthConfig()).toEqual({ domain: 'tenant.auth0.com', clientId: 'client-123' });
  });
});

describe('getAccountDeleteUrl', () => {
  beforeEach(() => {
    delete process.env.EXPO_PUBLIC_ACCOUNT_DELETE_URL;
  });

  test('returns null when unset', () => {
    expect(getAccountDeleteUrl()).toBeNull();
  });

  test('returns the url when set', () => {
    process.env.EXPO_PUBLIC_ACCOUNT_DELETE_URL = 'https://api.example.com/account';
    expect(getAccountDeleteUrl()).toBe('https://api.example.com/account');
  });
});
