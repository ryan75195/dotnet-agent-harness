import { ReactNode } from 'react';

import { SignInScreen } from './SignInScreen';
import type { AuthState } from '../lib/auth/useAuth';

type AuthGateProps = {
  auth: AuthState;
  children: ReactNode;
};

export function AuthGate({ auth, children }: AuthGateProps) {
  if (auth.isAuthEnabled && !auth.isAuthenticated) {
    return <SignInScreen auth={auth} />;
  }
  return <>{children}</>;
}
