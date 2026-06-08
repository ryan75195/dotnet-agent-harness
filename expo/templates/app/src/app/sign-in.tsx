import { SignInScreen } from '../features/auth/SignInScreen';
import { useAuthContext } from '../lib/auth/AuthProvider';

export default function SignIn() {
  return <SignInScreen auth={useAuthContext()} />;
}
