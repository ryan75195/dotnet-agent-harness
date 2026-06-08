import { clearTokens, loadTokens, saveTokens, TokenSet } from '../tokenStore';

const sample: TokenSet = {
  accessToken: 'access',
  refreshToken: 'refresh',
  idToken: 'id',
  expiresAt: 1000
};

describe('tokenStore', () => {
  beforeEach(async () => {
    await clearTokens();
  });

  test('returns null when nothing is stored', async () => {
    expect(await loadTokens()).toBeNull();
  });

  test('round-trips a saved token set', async () => {
    await saveTokens(sample);
    expect(await loadTokens()).toEqual(sample);
  });

  test('clear removes the stored tokens', async () => {
    await saveTokens(sample);
    await clearTokens();
    expect(await loadTokens()).toBeNull();
  });
});
