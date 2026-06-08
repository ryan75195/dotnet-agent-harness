import * as SecureStore from 'expo-secure-store';

export type TokenSet = {
  accessToken: string;
  refreshToken: string | null;
  idToken: string | null;
  expiresAt: number;
};

const STORAGE_KEY = 'auth.tokens';

export async function saveTokens(tokens: TokenSet): Promise<void> {
  await SecureStore.setItemAsync(STORAGE_KEY, JSON.stringify(tokens));
}

export async function loadTokens(): Promise<TokenSet | null> {
  const raw = await SecureStore.getItemAsync(STORAGE_KEY);
  if (raw === null) {
    return null;
  }
  return JSON.parse(raw) as TokenSet;
}

export async function clearTokens(): Promise<void> {
  await SecureStore.deleteItemAsync(STORAGE_KEY);
}
