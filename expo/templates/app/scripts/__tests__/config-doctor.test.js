const { evaluateConfig, formatReport } = require('../config-doctor');

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

describe('formatReport', () => {
  test('silent when nothing to nudge', () => {
    expect(formatReport({ shouldNudge: false, critical: [], optional: [] })).toBe('');
  });

  test('nudge names the skill and the action and marks failures', () => {
    const result = evaluateConfig(goodInputs({ easWhoami: { status: 'logged-out', user: null } }));
    const report = formatReport(result);
    expect(report).toContain('first-run-setup');
    expect(report).toContain('ACTION:');
    expect(report).toContain('✗ EAS login');
  });

  test('lists only optional features still awaiting a decision', () => {
    const result = evaluateConfig(goodInputs({
      env: {},
      setupState: { version: 1, iosCredentials: 'provisioned', features: { payments: 'deferred' } }
    }));
    const report = formatReport(result);
    expect(report).toContain('Auth (Auth0)');
    expect(report).not.toContain('Payments (RevenueCat)');
  });
});

const fs = require('fs');
const os = require('os');
const path = require('path');
const { collectInputs, main } = require('../config-doctor');

function writeFixture(files) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'config-doctor-'));
  for (const [name, contents] of Object.entries(files)) {
    const full = path.join(dir, name);
    fs.mkdirSync(path.dirname(full), { recursive: true });
    fs.writeFileSync(full, contents);
  }
  return dir;
}

const APP_CONFIG = `require('./package.json');
const isProduction = process.env.NODE_ENV === 'production';
const bundleId = isProduction ? 'com.acme.myapp' : 'com.acme.myapp.dev';
module.exports = { expo: { ios: { bundleIdentifier: bundleId }, extra: { eas: { projectId: 'proj-xyz' } } } };
`;

const EAS_JSON = JSON.stringify({ build: { production: {} }, submit: { production: {} } });
const loggedIn = () => ({ status: 'logged-in', user: 'ryan' });

describe('collectInputs / main', () => {
  test('collectInputs reads app.config, eas.json, env and state', () => {
    const dir = writeFixture({
      'package.json': JSON.stringify({ version: '1.0.0' }),
      'app.config.js': APP_CONFIG,
      'eas.json': EAS_JSON,
      '.env.production': 'EXPO_PUBLIC_REVENUECAT_IOS_API_KEY=rc-key\n',
      '.claude/.setup-state.json': JSON.stringify({ version: 1, iosCredentials: 'provisioned', features: { auth: 'deferred', api: 'deferred' } })
    });
    const inputs = collectInputs({ cwd: dir, env: {}, runEas: loggedIn });
    expect(inputs.projectId).toBe('proj-xyz');
    expect(inputs.bundleId).toBe('com.acme.myapp');
    expect(inputs.hasProductionBuildProfile).toBe(true);
    expect(inputs.hasProductionSubmitProfile).toBe(true);
    expect(inputs.env.EXPO_PUBLIC_REVENUECAT_IOS_API_KEY).toBe('rc-key');
    expect(inputs.setupState.iosCredentials).toBe('provisioned');
  });

  test('main returns empty string for a fully configured project', () => {
    const dir = writeFixture({
      'package.json': JSON.stringify({ version: '1.0.0' }),
      'app.config.js': APP_CONFIG,
      'eas.json': EAS_JSON,
      '.env.production': 'EXPO_PUBLIC_REVENUECAT_IOS_API_KEY=rc\nEXPO_PUBLIC_AUTH0_DOMAIN=d\nEXPO_PUBLIC_AUTH0_CLIENT_ID=c\nEXPO_PUBLIC_API_BASE_URL=https://api\n',
      '.claude/.setup-state.json': JSON.stringify({ version: 1, iosCredentials: 'provisioned', features: {} })
    });
    expect(main({ cwd: dir, env: {}, runEas: loggedIn })).toBe('');
  });

  test('main nudges when projectId is missing', () => {
    const dir = writeFixture({
      'package.json': JSON.stringify({ version: '1.0.0' }),
      'app.config.js': `module.exports = { expo: { ios: { bundleIdentifier: 'com.acme.myapp' }, extra: {} } };`,
      'eas.json': EAS_JSON
    });
    const report = main({ cwd: dir, env: {}, runEas: loggedIn });
    expect(report).toContain('EAS project linked');
    expect(report).toContain('ACTION:');
  });
});
