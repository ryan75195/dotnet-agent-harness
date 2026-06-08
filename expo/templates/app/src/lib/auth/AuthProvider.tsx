import { createContext, ReactNode, useContext, useEffect } from 'react';

import { syncPurchasesIdentity } from './linkPurchases';
import { AuthState, useAuth } from './useAuth';

const AuthContext = createContext<AuthState | null>(null);

type AuthProviderProps = {
  children: ReactNode;
};

export function AuthProvider({ children }: AuthProviderProps) {
  const auth = useAuth();

  useEffect(() => {
    syncPurchasesIdentity(auth.user);
  }, [auth.user]);

  return <AuthContext.Provider value={auth}>{children}</AuthContext.Provider>;
}

export function useAuthContext(): AuthState {
  const value = useContext(AuthContext);
  if (value === null) {
    throw new Error('useAuthContext must be used within an AuthProvider');
  }
  return value;
}
