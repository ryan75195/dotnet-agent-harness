import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import * as AppleAuthentication from 'expo-apple-authentication';

import { AuthUser, fetchUserInfo, isExpired, loginWithAuth0, refreshTokens } from './authClient';
import { getAuthConfig } from './authConfig';
import { clearTokens, loadTokens, saveTokens, TokenSet } from './tokenStore';

export type AuthState = {
  isAuthEnabled: boolean;
  isAuthenticated: boolean;
  isLoading: boolean;
  user: AuthUser | null;
  signIn: () => Promise<void>;
  signInWithApple: () => Promise<void>;
  signOut: () => Promise<void>;
  getToken: () => Promise<string | null>;
};

function appleName(fullName: AppleAuthentication.AppleAuthenticationFullName | null): string | null {
  if (!fullName) {
    return null;
  }
  const parts = [fullName.givenName, fullName.familyName].filter(Boolean);
  return parts.length > 0 ? parts.join(' ') : null;
}

export function useAuth(): AuthState {
  const config = useMemo(() => getAuthConfig(), []);
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(config !== null);
  const tokensRef = useRef<TokenSet | null>(null);

  const adopt = useCallback(async (tokens: TokenSet, domain: string) => {
    tokensRef.current = tokens;
    await saveTokens(tokens);
    const profile = await fetchUserInfo(domain, tokens.accessToken);
    setUser(profile);
    setIsAuthenticated(true);
  }, []);

  useEffect(() => {
    if (config === null) {
      return;
    }
    let active = true;
    const restore = async () => {
      try {
        const stored = await loadTokens();
        if (!stored) {
          return;
        }
        tokensRef.current = stored;
        let usable = stored;
        if (isExpired(stored, Date.now())) {
          if (!stored.refreshToken) {
            await clearTokens();
            return;
          }
          usable = await refreshTokens(config, stored.refreshToken, Date.now());
        }
        if (active) {
          await adopt(usable, config.domain);
        }
      } catch {
        await clearTokens();
      } finally {
        if (active) {
          setIsLoading(false);
        }
      }
    };
    restore();
    return () => {
      active = false;
    };
  }, [config, adopt]);

  const signIn = useCallback(async () => {
    if (config === null) {
      return;
    }
    const tokens = await loginWithAuth0(config, Date.now());
    if (!tokens) {
      return;
    }
    await adopt(tokens, config.domain);
  }, [config, adopt]);

  const signInWithApple = useCallback(async () => {
    if (config === null) {
      return;
    }
    try {
      const credential = await AppleAuthentication.signInAsync({
        requestedScopes: [
          AppleAuthentication.AppleAuthenticationScope.FULL_NAME,
          AppleAuthentication.AppleAuthenticationScope.EMAIL
        ]
      });
      const tokens: TokenSet = {
        accessToken: credential.identityToken ?? '',
        refreshToken: null,
        idToken: credential.identityToken ?? null,
        expiresAt: Date.now() + 3600 * 1000
      };
      tokensRef.current = tokens;
      await saveTokens(tokens);
      setUser({ id: credential.user, email: credential.email ?? null, name: appleName(credential.fullName) });
      setIsAuthenticated(true);
    } catch {
      return;
    }
  }, [config]);

  const signOut = useCallback(async () => {
    await clearTokens();
    tokensRef.current = null;
    setUser(null);
    setIsAuthenticated(false);
  }, []);

  const getToken = useCallback(async () => {
    const current = tokensRef.current;
    if (config === null || !current) {
      return null;
    }
    if (!isExpired(current, Date.now())) {
      return current.accessToken;
    }
    if (!current.refreshToken) {
      return null;
    }
    const refreshed = await refreshTokens(config, current.refreshToken, Date.now());
    tokensRef.current = refreshed;
    await saveTokens(refreshed);
    return refreshed.accessToken;
  }, [config]);

  return {
    isAuthEnabled: config !== null,
    isAuthenticated,
    isLoading,
    user,
    signIn,
    signInWithApple,
    signOut,
    getToken
  };
}
