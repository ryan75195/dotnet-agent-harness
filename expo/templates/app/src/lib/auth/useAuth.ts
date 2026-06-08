import { MutableRefObject, useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { appleSignIn, passwordSignIn, resolveToken, restoreSession, Session } from './authActions';
import { AuthUser } from './authClient';
import { getAuthConfig } from './authConfig';
import { clearTokens, TokenSet } from './tokenStore';

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

type Setter<T> = React.Dispatch<React.SetStateAction<T>>;

function applySession(
  session: Session | null,
  tokensRef: MutableRefObject<TokenSet | null>,
  setUser: Setter<AuthUser | null>,
  setIsAuthenticated: Setter<boolean>
): void {
  if (!session) {
    return;
  }
  tokensRef.current = session.tokens;
  setUser(session.user);
  setIsAuthenticated(true);
}

function useRestoreEffect(
  config: ReturnType<typeof getAuthConfig>,
  apply: (session: Session | null) => void,
  setIsLoading: Setter<boolean>
): void {
  useEffect(() => {
    if (config === null) {
      return;
    }
    let active = true;
    restoreSession(config)
      .then((session) => { if (active) { apply(session); } })
      .catch(() => clearTokens())
      .finally(() => { if (active) { setIsLoading(false); } });
    return () => { active = false; };
  }, [config, apply, setIsLoading]);
}

export function useAuth(): AuthState {
  const config = useMemo(() => getAuthConfig(), []);
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(config !== null);
  const tokensRef = useRef<TokenSet | null>(null);

  const apply = useCallback(
    (session: Session | null) => applySession(session, tokensRef, setUser, setIsAuthenticated),
    []
  );

  useRestoreEffect(config, apply, setIsLoading);

  const signIn = useCallback(async () => {
    if (config !== null) { apply(await passwordSignIn(config)); }
  }, [config, apply]);

  const signInWithApple = useCallback(async () => {
    if (config !== null) { apply(await appleSignIn()); }
  }, [config, apply]);

  const signOut = useCallback(async () => {
    await clearTokens();
    tokensRef.current = null;
    setUser(null);
    setIsAuthenticated(false);
  }, []);

  const getToken = useCallback(async () => {
    const current = tokensRef.current;
    if (config === null || !current) { return null; }
    const resolved = await resolveToken(config, current);
    if (!resolved) { return null; }
    tokensRef.current = resolved.tokens;
    return resolved.token;
  }, [config]);

  return { isAuthEnabled: config !== null, isAuthenticated, isLoading, user, signIn, signInWithApple, signOut, getToken };
}
