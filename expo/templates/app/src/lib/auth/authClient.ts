import * as AuthSession from 'expo-auth-session';

import { AuthConfig } from './authConfig';
import { TokenSet } from './tokenStore';

export type AuthUser = {
  id: string;
  email: string | null;
  name: string | null;
};

const EXPIRY_SKEW_MS = 60_000;

export function isExpired(tokens: TokenSet, nowMs: number): boolean {
  return nowMs >= tokens.expiresAt - EXPIRY_SKEW_MS;
}

function toTokenSet(response: AuthSession.TokenResponse, nowMs: number): TokenSet {
  return {
    accessToken: response.accessToken,
    refreshToken: response.refreshToken ?? null,
    idToken: response.idToken ?? null,
    expiresAt: nowMs + (response.expiresIn ?? 0) * 1000
  };
}

export async function fetchUserInfo(domain: string, accessToken: string): Promise<AuthUser> {
  const response = await fetch(`https://${domain}/userinfo`, {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  const profile = (await response.json()) as { sub: string; email?: string; name?: string };
  return {
    id: profile.sub,
    email: profile.email ?? null,
    name: profile.name ?? null
  };
}

export async function loginWithAuth0(config: AuthConfig, nowMs: number): Promise<TokenSet | null> {
  const discovery = await AuthSession.fetchDiscoveryAsync(`https://${config.domain}`);
  const redirectUri = AuthSession.makeRedirectUri();
  const request = new AuthSession.AuthRequest({
    clientId: config.clientId,
    redirectUri,
    scopes: ['openid', 'profile', 'email', 'offline_access'],
    usePKCE: true
  });
  const result = await request.promptAsync(discovery);
  if (result.type !== 'success') {
    return null;
  }
  const code = result.params['code'];
  if (code === undefined) {
    return null;
  }
  const response = await AuthSession.exchangeCodeAsync(
    {
      clientId: config.clientId,
      code,
      redirectUri,
      extraParams: { code_verifier: request.codeVerifier ?? '' }
    },
    discovery
  );
  return toTokenSet(response, nowMs);
}

export async function refreshTokens(config: AuthConfig, refreshToken: string, nowMs: number): Promise<TokenSet> {
  const discovery = await AuthSession.fetchDiscoveryAsync(`https://${config.domain}`);
  const response = await AuthSession.refreshAsync(
    { clientId: config.clientId, refreshToken },
    discovery
  );
  return toTokenSet(response, nowMs);
}
