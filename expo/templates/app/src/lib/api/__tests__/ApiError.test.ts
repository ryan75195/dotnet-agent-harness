import { ApiError } from '../ApiError';

describe('ApiError', () => {
  test('carries status and body and is an Error', () => {
    const error = new ApiError(404, { message: 'nope' });
    expect(error).toBeInstanceOf(Error);
    expect(error.status).toBe(404);
    expect(error.body).toEqual({ message: 'nope' });
    expect(error.name).toBe('ApiError');
  });
});
