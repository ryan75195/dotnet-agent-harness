import type { AuthUser } from '../authClient';

test('AuthUser shape is usable', () => {
  const user: AuthUser = { id: 'x', email: null, name: null };
  expect(user.id).toBe('x');
});
