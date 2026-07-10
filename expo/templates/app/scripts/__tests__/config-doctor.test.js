const { evaluateConfig } = require('../config-doctor');

function goodInputs(overrides) {
  return {
    projectId: 'proj-123',
    bundleId: 'com.acme.myapp',
    hasProductionBuildProfile: true,
    hasProductionSubmitProfile: true,
    easWhoami: { status: 'logged-in', user: 'ryan' },
    env: {
      EXPO_PUBLIC_REVENUECAT_IOS_API_KEY: 'rc',
      EXPO_PUBLIC_AUTH0_DOMAIN: 'd',
      EXPO_PUBLIC_AUTH0_CLIENT_ID: 'c',
      EXPO_PUBLIC_API_BASE_URL: 'https://api'
    },
    setupState: { version: 1, iosCredentials: 'provisioned', features: {} },
    ...overrides
  };
}

describe('evaluateConfig', () => {
  test('fully configured project does not nudge', () => {
    const result = evaluateConfig(goodInputs());
    expect(result.shouldNudge).toBe(false);
    expect(result.allCriticalPass).toBe(true);
    expect(result.allOptionalDecided).toBe(true);
  });

  test('logged out fails the eas-login critical check', () => {
    const result = evaluateConfig(goodInputs({ easWhoami: { status: 'logged-out', user: null } }));
    expect(result.critical.find((r) => r.key === 'eas-login').ok).toBe(false);
    expect(result.shouldNudge).toBe(true);
  });

  test('unknown login status fails but marks verify', () => {
    const result = evaluateConfig(goodInputs({ easWhoami: { status: 'unknown', user: null } }));
    const login = result.critical.find((r) => r.key === 'eas-login');
    expect(login.ok).toBe(false);
    expect(login.detail).toMatch(/verify|offline|timeout|re-check/i);
  });

  test('missing projectId fails', () => {
    const result = evaluateConfig(goodInputs({ projectId: null }));
    expect(result.critical.find((r) => r.key === 'eas-project-id').ok).toBe(false);
  });

  test('placeholder bundle id fails', () => {
    const result = evaluateConfig(goodInputs({ bundleId: 'com.example.apptemplate' }));
    expect(result.critical.find((r) => r.key === 'bundle-id').ok).toBe(false);
  });

  test('missing submit profile fails production profiles', () => {
    const result = evaluateConfig(goodInputs({ hasProductionSubmitProfile: false }));
    expect(result.critical.find((r) => r.key === 'eas-production-profiles').ok).toBe(false);
  });

  test('missing credentials marker fails', () => {
    const result = evaluateConfig(goodInputs({ setupState: { version: 1, features: {} } }));
    expect(result.critical.find((r) => r.key === 'ios-credentials').ok).toBe(false);
  });

  test('undecided optional feature nudges even when critical passes', () => {
    const result = evaluateConfig(goodInputs({ env: {}, setupState: { version: 1, iosCredentials: 'provisioned', features: {} } }));
    expect(result.allCriticalPass).toBe(true);
    expect(result.allOptionalDecided).toBe(false);
    expect(result.shouldNudge).toBe(true);
  });

  test('deferred optional decisions satisfy allOptionalDecided', () => {
    const result = evaluateConfig(goodInputs({ env: {}, setupState: { version: 1, iosCredentials: 'provisioned', features: { payments: 'deferred', auth: 'deferred', api: 'deferred' } } }));
    expect(result.allOptionalDecided).toBe(true);
    expect(result.shouldNudge).toBe(false);
  });

  test('configured optional feature counts as decided without a state entry', () => {
    const result = evaluateConfig(goodInputs({ env: { EXPO_PUBLIC_REVENUECAT_IOS_API_KEY: 'rc' }, setupState: { version: 1, iosCredentials: 'provisioned', features: { auth: 'deferred', api: 'deferred' } } }));
    const payments = result.optional.find((o) => o.key === 'payments');
    expect(payments.configured).toBe(true);
    expect(payments.needsDecision).toBe(false);
    expect(result.shouldNudge).toBe(false);
  });
});
