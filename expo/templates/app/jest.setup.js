jest.mock('react-native-purchases', () => ({
  __esModule: true,
  default: {
    configure: jest.fn(),
    getCustomerInfo: jest.fn().mockResolvedValue({ entitlements: { active: {} } }),
    addCustomerInfoUpdateListener: jest.fn(),
    getOfferings: jest.fn().mockResolvedValue({ current: null }),
    purchasePackage: jest.fn(),
    logIn: jest.fn().mockResolvedValue({}),
    logOut: jest.fn().mockResolvedValue({})
  }
}));

jest.mock('expo-secure-store', () => {
  const store = {};
  return {
    setItemAsync: jest.fn(async (key, value) => {
      store[key] = value;
    }),
    getItemAsync: jest.fn(async (key) => (key in store ? store[key] : null)),
    deleteItemAsync: jest.fn(async (key) => {
      delete store[key];
    })
  };
});

jest.mock('expo-auth-session', () => ({
  makeRedirectUri: jest.fn(() => 'apptemplate://redirect'),
  fetchDiscoveryAsync: jest.fn().mockResolvedValue({
    authorizationEndpoint: 'https://tenant.auth0.com/authorize',
    tokenEndpoint: 'https://tenant.auth0.com/oauth/token'
  }),
  exchangeCodeAsync: jest.fn().mockResolvedValue({
    accessToken: 'access-1',
    refreshToken: 'refresh-1',
    idToken: 'id-1',
    expiresIn: 3600
  }),
  refreshAsync: jest.fn().mockResolvedValue({
    accessToken: 'access-2',
    refreshToken: 'refresh-2',
    idToken: 'id-2',
    expiresIn: 3600
  }),
  AuthRequest: class {
    constructor(options) {
      this.options = options;
      this.codeVerifier = 'verifier-1';
    }
    async promptAsync() {
      return { type: 'success', params: { code: 'code-1' } };
    }
  }
}));

jest.mock('expo-apple-authentication', () => ({
  signInAsync: jest.fn(),
  isAvailableAsync: jest.fn().mockResolvedValue(true),
  AppleAuthenticationScope: { FULL_NAME: 0, EMAIL: 1 },
  AppleAuthenticationButtonType: { SIGN_IN: 0 },
  AppleAuthenticationButtonStyle: { BLACK: 0 },
  AppleAuthenticationButton: 'AppleAuthenticationButton'
}));
