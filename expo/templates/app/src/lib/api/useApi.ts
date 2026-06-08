import { useMemo } from 'react';

import { useAuthContext } from '../auth/AuthProvider';
import { ApiClient, createApiClient } from './apiClient';

export function useApi(): ApiClient {
  const auth = useAuthContext();
  return useMemo(() => createApiClient(auth.getToken), [auth]);
}
