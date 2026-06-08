import { ApiError } from './ApiError';

export type TokenProvider = () => Promise<string | null>;

export type ApiClient = {
  get: <T>(path: string) => Promise<T>;
  post: <T>(path: string, body?: unknown) => Promise<T>;
  put: <T>(path: string, body?: unknown) => Promise<T>;
  del: <T>(path: string) => Promise<T>;
};

export function getApiBaseUrl(): string | null {
  const url = process.env.EXPO_PUBLIC_API_BASE_URL ?? '';
  return url === '' ? null : url;
}

async function parseBody(response: Response): Promise<unknown> {
  if (response.status === 204) {
    return undefined;
  }
  const text = await response.text();
  return text === '' ? undefined : JSON.parse(text);
}

async function request<T>(getToken: TokenProvider, method: string, path: string, body?: unknown): Promise<T> {
  const baseUrl = getApiBaseUrl();
  if (baseUrl === null) {
    throw new Error('EXPO_PUBLIC_API_BASE_URL is not set');
  }
  const token = await getToken();
  const headers: Record<string, string> = {};
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }
  if (token !== null) {
    headers.Authorization = `Bearer ${token}`;
  }
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  const parsed = await parseBody(response);
  if (!response.ok) {
    throw new ApiError(response.status, parsed);
  }
  return parsed as T;
}

export function createApiClient(getToken: TokenProvider): ApiClient {
  return {
    get: <T>(path: string) => request<T>(getToken, 'GET', path),
    post: <T>(path: string, body?: unknown) => request<T>(getToken, 'POST', path, body),
    put: <T>(path: string, body?: unknown) => request<T>(getToken, 'PUT', path, body),
    del: <T>(path: string) => request<T>(getToken, 'DELETE', path)
  };
}
