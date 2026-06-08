import { ApiError } from '../ApiError';
import { createApiClient, getApiBaseUrl } from '../apiClient';

function jsonResponse(status: number, data: unknown) {
  return {
    ok: status >= 200 && status < 300,
    status,
    text: async () => JSON.stringify(data)
  } as unknown as Response;
}

describe('getApiBaseUrl', () => {
  beforeEach(() => {
    delete process.env.EXPO_PUBLIC_API_BASE_URL;
  });

  test('null when unset', () => {
    expect(getApiBaseUrl()).toBeNull();
  });

  test('returns the url when set', () => {
    process.env.EXPO_PUBLIC_API_BASE_URL = 'https://api.example.com';
    expect(getApiBaseUrl()).toBe('https://api.example.com');
  });
});

describe('createApiClient', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    process.env.EXPO_PUBLIC_API_BASE_URL = 'https://api.example.com';
  });

  test('GET attaches the bearer token and returns parsed json', async () => {
    global.fetch = jest.fn().mockResolvedValue(jsonResponse(200, { id: 1 })) as unknown as typeof fetch;
    const client = createApiClient(async () => 'tok-1');
    const result = await client.get('/me');
    expect(result).toEqual({ id: 1 });
    expect(global.fetch).toHaveBeenCalledWith('https://api.example.com/me', {
      method: 'GET',
      headers: { Authorization: 'Bearer tok-1' },
      body: undefined
    });
  });

  test('omits the Authorization header when there is no token', async () => {
    global.fetch = jest.fn().mockResolvedValue(jsonResponse(200, {})) as unknown as typeof fetch;
    const client = createApiClient(async () => null);
    await client.get('/public');
    expect(global.fetch).toHaveBeenCalledWith('https://api.example.com/public', {
      method: 'GET',
      headers: {},
      body: undefined
    });
  });

  test('POST serializes the body with a json content type', async () => {
    global.fetch = jest.fn().mockResolvedValue(jsonResponse(201, { ok: true })) as unknown as typeof fetch;
    const client = createApiClient(async () => 'tok-1');
    await client.post('/items', { name: 'x' });
    expect(global.fetch).toHaveBeenCalledWith('https://api.example.com/items', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: 'Bearer tok-1' },
      body: JSON.stringify({ name: 'x' })
    });
  });

  test('PUT serializes the body and returns json', async () => {
    global.fetch = jest.fn().mockResolvedValue(jsonResponse(200, { updated: true })) as unknown as typeof fetch;
    const client = createApiClient(async () => 'tok-1');
    expect(await client.put('/items/1', { name: 'y' })).toEqual({ updated: true });
  });

  test('returns undefined for a 204 response', async () => {
    global.fetch = jest.fn().mockResolvedValue({ ok: true, status: 204, text: async () => '' } as unknown as Response) as unknown as typeof fetch;
    const client = createApiClient(async () => 'tok-1');
    expect(await client.del('/items/1')).toBeUndefined();
  });

  test('returns undefined for a 200 with an empty body', async () => {
    global.fetch = jest.fn().mockResolvedValue({ ok: true, status: 200, text: async () => '' } as unknown as Response) as unknown as typeof fetch;
    const client = createApiClient(async () => 'tok-1');
    expect(await client.get('/ping')).toBeUndefined();
  });

  test('throws ApiError with status and body on a non-2xx response', async () => {
    global.fetch = jest.fn().mockResolvedValue(jsonResponse(404, { message: 'nope' })) as unknown as typeof fetch;
    const client = createApiClient(async () => 'tok-1');
    await expect(client.get('/missing')).rejects.toBeInstanceOf(ApiError);
    await expect(client.get('/missing')).rejects.toMatchObject({ status: 404, body: { message: 'nope' } });
  });

  test('throws when the base url is unset', async () => {
    delete process.env.EXPO_PUBLIC_API_BASE_URL;
    const client = createApiClient(async () => 'tok-1');
    await expect(client.get('/me')).rejects.toThrow('EXPO_PUBLIC_API_BASE_URL is not set');
  });
});
