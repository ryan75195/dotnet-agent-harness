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

export function getAccountDeleteUrl(): string | null {
  const url = process.env.EXPO_PUBLIC_ACCOUNT_DELETE_URL ?? '';
  return url === '' ? null : url;
}
