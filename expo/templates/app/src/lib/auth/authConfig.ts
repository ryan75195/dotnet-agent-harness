export type AuthConfig = {
  domain: string;
  clientId: string;
};

export function getAuthConfig(): AuthConfig | null {
  const domain = process.env.EXPO_PUBLIC_AUTH0_DOMAIN ?? '';
  const clientId = process.env.EXPO_PUBLIC_AUTH0_CLIENT_ID ?? '';
  if (domain === '' || clientId === '') {
    return null;
  }
  return { domain, clientId };
}

export function isAuthEnabled(): boolean {
  return getAuthConfig() !== null;
}
