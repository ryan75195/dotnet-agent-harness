import * as AppleAuthentication from 'expo-apple-authentication';

import { AuthUser, fetchUserInfo, isExpired, loginWithAuth0, refreshTokens } from './authClient';
import { AuthConfig } from './authConfig';
import { clearTokens, loadTokens, saveTokens, TokenSet } from './tokenStore';

export type Session = {
  tokens: TokenSet;
  user: AuthUser;
};

export type ResolvedToken = {
  token: string;
  tokens: TokenSet;
};

function appleName(fullName: AppleAuthentication.AppleAuthenticationFullName | null): string | null {
  if (!fullName) {
    return null;
  }
  const parts = [fullName.givenName, fullName.familyName].filter(Boolean);
  return parts.length > 0 ? parts.join(' ') : null;
}

export async function restoreSession(config: AuthConfig): Promise<Session | null> {
  const stored = await loadTokens();
  if (!stored) {
    return null;
  }
  let usable = stored;
  if (isExpired(stored, Date.now())) {
    if (!stored.refreshToken) {
      await clearTokens();
      return null;
    }
    usable = await refreshTokens(config, stored.refreshToken, Date.now());
  }
  await saveTokens(usable);
  const user = await fetchUserInfo(config.domain, usable.accessToken);
  return { tokens: usable, user };
}

export async function passwordSignIn(config: AuthConfig): Promise<Session | null> {
  const tokens = await loginWithAuth0(config, Date.now());
  if (!tokens) {
    return null;
  }
  await saveTokens(tokens);
  const user = await fetchUserInfo(config.domain, tokens.accessToken);
  return { tokens, user };
}

export async function appleSignIn(): Promise<Session | null> {
  try {
    const credential = await AppleAuthentication.signInAsync({
      requestedScopes: [
        AppleAuthentication.AppleAuthenticationScope.FULL_NAME,
        AppleAuthentication.AppleAuthenticationScope.EMAIL
      ]
    });
    if (!credential.identityToken) {
      return null;
    }
    const tokens: TokenSet = {
      accessToken: credential.identityToken,
      refreshToken: null,
      idToken: credential.identityToken,
      expiresAt: Date.now() + 3600 * 1000
    };
    await saveTokens(tokens);
    return {
      tokens,
      user: { id: credential.user, email: credential.email ?? null, name: appleName(credential.fullName) }
    };
  } catch {
    return null;
  }
}

export async function resolveToken(config: AuthConfig, current: TokenSet): Promise<ResolvedToken | null> {
  if (!isExpired(current, Date.now())) {
    return { token: current.accessToken, tokens: current };
  }
  if (!current.refreshToken) {
    return null;
  }
  const refreshed = await refreshTokens(config, current.refreshToken, Date.now());
  await saveTokens(refreshed);
  return { token: refreshed.accessToken, tokens: refreshed };
}
