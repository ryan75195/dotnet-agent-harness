# Expo First-Run Config Setup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a SessionStart config detector + `first-run-setup` interview skill to the Expo template so a scaffolded app reaches EAS build/deploy readiness (and records optional payments/auth decisions) instead of hitting setup walls at build time.

**Architecture:** A logic-free bash SessionStart hook shells into a new Node detector (`scripts/config-doctor.js`) built as a pure core (`evaluateConfig`/`formatReport`) plus a thin IO shell (`collectInputs`/`main`). When critical config is missing the detector prints a nudge (injected into session context) telling Claude to run the `first-run-setup` skill, which does the interactive Q&A and writes `app.config.js`, `.env.*`, and `.claude/.setup-state.json`. When everything is satisfied the detector prints nothing.

**Tech Stack:** Node.js (CommonJS, matching `scripts/submission-doctor.js`), Jest (`jest-expo` preset), bash hook, Claude Code SessionStart hook, PowerShell smoke test.

## Global Constraints

- **This plan edits the TEMPLATE repo** at `expo/templates/app/`. All implementation commits go to the current branch `feat/expo-first-run-config-setup` in `agent-project-templates`. The scaffolded-app dev lifecycle (issue→branch→PR, no edits on `main`) is a *runtime* behavior of scaffolded apps and does NOT apply to implementing this plan.
- **Run all `npm`/`npx` commands from `expo/templates/app/`.** Its `node_modules` is already installed; if absent, run `npm install` there once.
- **The detector must never fail the session:** `scripts/config-doctor.js` and the hook always exit 0; all IO is wrapped so a broken/missing file, missing `eas` CLI, or offline network degrades to a best-effort nudge, never a throw.
- **`scripts/**` is eslint-ignored and excluded from Jest coverage** (`eslint.config.js` ignores + `jest.config.js` `collectCoverageFrom`), and `check-test-files` only gates `src/lib`+`src/features`. So `config-doctor.js` has no lint/coverage gate; its test in `scripts/__tests__/` is still picked up by `testMatch: ['**/__tests__/**/*.test.[jt]s?(x)']`.
- **`EXPO_PUBLIC_*` values are written to BOTH `.env.local` and `.env.production`** (mirrors the `revenuecat-setup` dual-write).
- **`eas whoami` is checked live with a ~3s timeout**, degrading to status `unknown` on timeout / `ENOENT` / signal.
- **`.claude/.setup-state.json` schema:** `{ "version": 1, "iosCredentials"?: "provisioned", "features": { "<key>": "enabled" | "deferred" } }`. Gitignored.
- **Tier 1 critical checks (keys):** `eas-login`, `eas-project-id`, `eas-production-profiles` (needs both `build.production` and `submit.production`), `bundle-id` (fails if it contains `com.example.`), `ios-credentials` (satisfied by the `iosCredentials: "provisioned"` state marker). **Tier 2 optional features (keys):** `payments` (`EXPO_PUBLIC_REVENUECAT_IOS_API_KEY`), `auth` (`EXPO_PUBLIC_AUTH0_DOMAIN` + `EXPO_PUBLIC_AUTH0_CLIENT_ID`), `api` (`EXPO_PUBLIC_API_BASE_URL`).
- **Design refinement to note (deviation from spec's "Tier 1 always live"):** iOS credentials CANNOT be cheaply/non-interactively verified at session start, so the detector reads the `iosCredentials` state marker written by the skill after a successful `eas credentials` run. The *skill* performs the live provisioning/verification at run time (which is also build time), preserving trustworthiness where it matters. Every other Tier 1 item is checked live.

---

### Task 1: Detector core — `evaluateConfig`

**Files:**
- Create: `expo/templates/app/scripts/config-doctor.js`
- Test: `expo/templates/app/scripts/__tests__/config-doctor.test.js`

**Interfaces:**
- Produces:
  - `evaluateConfig(inputs) => { critical: CheckResult[], optional: OptionalResult[], allCriticalPass: boolean, allOptionalDecided: boolean, shouldNudge: boolean }`
  - `OPTIONAL_FEATURES: Array<{ key: string, label: string, requiredEnv: string[] }>`
  - `CheckResult = { key: string, label: string, ok: boolean, detail: string }`
  - `OptionalResult = { key: string, label: string, configured: boolean, decision: 'enabled'|'deferred'|null, needsDecision: boolean }`
  - `inputs = { projectId: string|null, bundleId: string|null, hasProductionBuildProfile: boolean, hasProductionSubmitProfile: boolean, easWhoami: { status: 'logged-in'|'logged-out'|'unknown', user: string|null }, env: Record<string,string|undefined>, setupState: { version: number, iosCredentials?: string, features?: Record<string,'enabled'|'deferred'> } | null }`

- [ ] **Step 1: Write the failing tests**

Create `expo/templates/app/scripts/__tests__/config-doctor.test.js`:

```js
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd expo/templates/app && npx jest scripts/__tests__/config-doctor.test.js`
Expected: FAIL — `Cannot find module '../config-doctor'`.

- [ ] **Step 3: Create `config-doctor.js` with the core logic**

Create `expo/templates/app/scripts/config-doctor.js`:

```js
const OPTIONAL_FEATURES = [
  { key: 'payments', label: 'Payments (RevenueCat)', requiredEnv: ['EXPO_PUBLIC_REVENUECAT_IOS_API_KEY'] },
  { key: 'auth', label: 'Auth (Auth0)', requiredEnv: ['EXPO_PUBLIC_AUTH0_DOMAIN', 'EXPO_PUBLIC_AUTH0_CLIENT_ID'] },
  { key: 'api', label: 'API base URL', requiredEnv: ['EXPO_PUBLIC_API_BASE_URL'] }
];

function evaluateCritical(inputs) {
  const results = [];
  const whoami = inputs.easWhoami || { status: 'unknown', user: null };
  if (whoami.status === 'logged-in') {
    results.push({ key: 'eas-login', label: 'EAS login', ok: true, detail: whoami.user ? `logged in as ${whoami.user}` : 'logged in' });
  } else if (whoami.status === 'logged-out') {
    results.push({ key: 'eas-login', label: 'EAS login', ok: false, detail: 'not logged in — run `eas login`' });
  } else {
    results.push({ key: 'eas-login', label: 'EAS login', ok: false, detail: 'could not verify (offline/timeout) — first-run-setup will re-check' });
  }

  results.push({
    key: 'eas-project-id',
    label: 'EAS project linked',
    ok: Boolean(inputs.projectId),
    detail: inputs.projectId ? `projectId ${inputs.projectId}` : 'extra.eas.projectId not set — run `eas init`'
  });

  const profilesOk = Boolean(inputs.hasProductionBuildProfile) && Boolean(inputs.hasProductionSubmitProfile);
  results.push({
    key: 'eas-production-profiles',
    label: 'EAS production profiles',
    ok: profilesOk,
    detail: profilesOk ? 'build + submit profiles present' : 'eas.json missing build.production or submit.production'
  });

  const bundleId = inputs.bundleId || '';
  const bundleOk = bundleId.length > 0 && !bundleId.includes('com.example.');
  results.push({
    key: 'bundle-id',
    label: 'Production bundle identifier',
    ok: bundleOk,
    detail: bundleOk ? bundleId : `still a placeholder (${bundleId || 'unset'})`
  });

  const credentialsOk = Boolean(inputs.setupState && inputs.setupState.iosCredentials === 'provisioned');
  results.push({
    key: 'ios-credentials',
    label: 'iOS credentials',
    ok: credentialsOk,
    detail: credentialsOk ? 'provisioned' : 'not yet provisioned — first-run-setup runs `eas credentials`'
  });

  return results;
}

function isConfigured(feature, env) {
  return feature.requiredEnv.every((name) => Boolean(env && env[name]));
}

function evaluateOptional(inputs) {
  const env = inputs.env || {};
  const features = (inputs.setupState && inputs.setupState.features) || {};
  return OPTIONAL_FEATURES.map((feature) => {
    const configured = isConfigured(feature, env);
    const decision = configured ? 'enabled' : (features[feature.key] || null);
    return { key: feature.key, label: feature.label, configured, decision, needsDecision: !configured && !decision };
  });
}

function evaluateConfig(inputs) {
  const critical = evaluateCritical(inputs);
  const optional = evaluateOptional(inputs);
  const allCriticalPass = critical.every((r) => r.ok);
  const allOptionalDecided = optional.every((o) => !o.needsDecision);
  return { critical, optional, allCriticalPass, allOptionalDecided, shouldNudge: !allCriticalPass || !allOptionalDecided };
}

module.exports = { evaluateConfig, OPTIONAL_FEATURES };
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `cd expo/templates/app && npx jest scripts/__tests__/config-doctor.test.js`
Expected: PASS (all `describe('evaluateConfig')` tests green).

- [ ] **Step 5: Commit**

```bash
git add expo/templates/app/scripts/config-doctor.js expo/templates/app/scripts/__tests__/config-doctor.test.js
git commit -m "Add config-doctor evaluateConfig core for Expo first-run setup"
```

---

### Task 2: Detector output — `formatReport`

**Files:**
- Modify: `expo/templates/app/scripts/config-doctor.js`
- Modify: `expo/templates/app/scripts/__tests__/config-doctor.test.js`

**Interfaces:**
- Consumes: `evaluateConfig(inputs)` result shape from Task 1.
- Produces: `formatReport(result) => string` — returns `''` when `result.shouldNudge` is false; otherwise a multi-line nudge block containing the literal substrings `first-run-setup` and `ACTION:`, a `Critical (build & deploy):` section listing every critical check with `✓`/`✗`, and (only when present) an `Optional features awaiting a decision:` section listing only `needsDecision` items.

- [ ] **Step 1: Write the failing tests**

Append to `expo/templates/app/scripts/__tests__/config-doctor.test.js` (the top-level `goodInputs` helper and the `evaluateConfig` import from Task 1 are already in scope in this same file):

```js
const { formatReport } = require('../config-doctor');

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
```

Note: `evaluateConfig` is already required at the top of the file from Task 1; add `formatReport` to that existing `require`, or add the `const { formatReport } = require('../config-doctor');` line as shown.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd expo/templates/app && npx jest scripts/__tests__/config-doctor.test.js -t formatReport`
Expected: FAIL — `formatReport is not a function`.

- [ ] **Step 3: Add `formatReport` and export it**

In `expo/templates/app/scripts/config-doctor.js`, add before the `module.exports` line:

```js
function formatCriticalLine(result) {
  const mark = result.ok ? '✓' : '✗';
  return `  ${mark} ${result.label} — ${result.detail}`;
}

function formatOptionalLine(option) {
  return `  • ${option.label} — awaiting a decision (enable or skip)`;
}

function formatReport(result) {
  if (!result.shouldNudge) {
    return '';
  }
  const lines = ['[first-run-setup] This project is not fully ready to build & deploy yet.', ''];
  lines.push('Critical (build & deploy):');
  for (const r of result.critical) {
    lines.push(formatCriticalLine(r));
  }
  const undecided = result.optional.filter((o) => o.needsDecision);
  if (undecided.length > 0) {
    lines.push('', 'Optional features awaiting a decision:');
    for (const o of undecided) {
      lines.push(formatOptionalLine(o));
    }
  }
  lines.push('', 'ACTION: Run the first-run-setup skill now to resolve the critical items and decide the optional ones. Do this before attempting any EAS build.');
  return lines.join('\n');
}
```

Update the export line to:

```js
module.exports = { evaluateConfig, formatReport, OPTIONAL_FEATURES };
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `cd expo/templates/app && npx jest scripts/__tests__/config-doctor.test.js`
Expected: PASS (both `evaluateConfig` and `formatReport` suites green).

- [ ] **Step 5: Commit**

```bash
git add expo/templates/app/scripts/config-doctor.js expo/templates/app/scripts/__tests__/config-doctor.test.js
git commit -m "Add config-doctor formatReport nudge output"
```

---

### Task 3: Detector IO shell — `collectInputs`, `defaultEasWhoami`, `main`, CLI

**Files:**
- Modify: `expo/templates/app/scripts/config-doctor.js`
- Modify: `expo/templates/app/scripts/__tests__/config-doctor.test.js`

**Interfaces:**
- Consumes: `evaluateConfig`, `formatReport`, `OPTIONAL_FEATURES` from Tasks 1–2.
- Produces:
  - `collectInputs({ cwd?, env?, runEas? }) => inputs` — reads `app.config.js` (loaded with `NODE_ENV='development'` and require-cache busting; strips a trailing `.dev` from the bundle id), `eas.json`, `.env.local` + `.env.production`, and `.claude/.setup-state.json`, all relative to `cwd`. `runEas` defaults to `defaultEasWhoami`.
  - `defaultEasWhoami() => { status, user }` — runs `eas whoami` with a 3s timeout; `ETIMEDOUT`/`ENOENT`/signal → `{ status: 'unknown', user: null }`, other error → `{ status: 'logged-out', user: null }`, success → `{ status: 'logged-in', user }`.
  - `main({ cwd?, env?, runEas? }) => string` — `formatReport(evaluateConfig(collectInputs(options)))`.
  - CLI tail: when run directly, writes a non-empty report to stdout and always exits 0.

- [ ] **Step 1: Write the failing tests**

Append to `expo/templates/app/scripts/__tests__/config-doctor.test.js`:

```js
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd expo/templates/app && npx jest scripts/__tests__/config-doctor.test.js -t "collectInputs / main"`
Expected: FAIL — `collectInputs is not a function`.

- [ ] **Step 3: Add the IO shell + CLI and update exports**

At the TOP of `expo/templates/app/scripts/config-doctor.js` add the requires:

```js
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
```

Add these functions before the `module.exports` line:

```js
function parseEnvFile(filePath) {
  if (!fs.existsSync(filePath)) {
    return {};
  }
  const out = {};
  for (const rawLine of fs.readFileSync(filePath, 'utf8').split('\n')) {
    const line = rawLine.trim();
    if (!line || line.startsWith('#')) {
      continue;
    }
    const eq = line.indexOf('=');
    if (eq === -1) {
      continue;
    }
    const key = line.slice(0, eq).trim();
    const value = line.slice(eq + 1).trim();
    if (key && value) {
      out[key] = value;
    }
  }
  return out;
}

function loadAppConfig(cwd) {
  const configPath = path.join(cwd, 'app.config.js');
  if (!fs.existsSync(configPath)) {
    return { projectId: null, bundleId: null };
  }
  const resolved = require.resolve(configPath);
  const previousEnv = process.env.NODE_ENV;
  process.env.NODE_ENV = 'development';
  delete require.cache[resolved];
  try {
    const mod = require(resolved);
    const expo = (mod && mod.expo) || {};
    const projectId = (((expo.extra || {}).eas) || {}).projectId || null;
    const rawBundle = ((expo.ios || {}).bundleIdentifier) || null;
    const bundleId = rawBundle ? rawBundle.replace(/\.dev$/, '') : null;
    return { projectId, bundleId };
  } catch (error) {
    return { projectId: null, bundleId: null };
  } finally {
    process.env.NODE_ENV = previousEnv;
    delete require.cache[resolved];
  }
}

function loadEasJson(cwd) {
  const easPath = path.join(cwd, 'eas.json');
  if (!fs.existsSync(easPath)) {
    return { hasProductionBuildProfile: false, hasProductionSubmitProfile: false };
  }
  try {
    const eas = JSON.parse(fs.readFileSync(easPath, 'utf8'));
    return {
      hasProductionBuildProfile: Boolean(eas.build && eas.build.production),
      hasProductionSubmitProfile: Boolean(eas.submit && eas.submit.production)
    };
  } catch (error) {
    return { hasProductionBuildProfile: false, hasProductionSubmitProfile: false };
  }
}

function loadSetupState(cwd) {
  const statePath = path.join(cwd, '.claude', '.setup-state.json');
  if (!fs.existsSync(statePath)) {
    return null;
  }
  try {
    return JSON.parse(fs.readFileSync(statePath, 'utf8'));
  } catch (error) {
    return null;
  }
}

function collectEnv(cwd, processEnv) {
  const merged = { ...parseEnvFile(path.join(cwd, '.env.local')), ...parseEnvFile(path.join(cwd, '.env.production')) };
  for (const feature of OPTIONAL_FEATURES) {
    for (const name of feature.requiredEnv) {
      if (processEnv && processEnv[name]) {
        merged[name] = processEnv[name];
      }
    }
  }
  return merged;
}

function defaultEasWhoami() {
  try {
    const out = execSync('eas whoami', { timeout: 3000, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] });
    const user = out.trim().split('\n').pop().trim();
    return { status: 'logged-in', user: user || null };
  } catch (error) {
    if (error.code === 'ETIMEDOUT' || error.code === 'ENOENT' || error.signal) {
      return { status: 'unknown', user: null };
    }
    return { status: 'logged-out', user: null };
  }
}

function collectInputs(options) {
  const cwd = (options && options.cwd) || process.cwd();
  const processEnv = (options && options.env) || process.env;
  const runEas = (options && options.runEas) || defaultEasWhoami;
  const appConfig = loadAppConfig(cwd);
  const easJson = loadEasJson(cwd);
  return {
    projectId: appConfig.projectId,
    bundleId: appConfig.bundleId,
    hasProductionBuildProfile: easJson.hasProductionBuildProfile,
    hasProductionSubmitProfile: easJson.hasProductionSubmitProfile,
    easWhoami: runEas(),
    env: collectEnv(cwd, processEnv),
    setupState: loadSetupState(cwd)
  };
}

function main(options) {
  return formatReport(evaluateConfig(collectInputs(options)));
}
```

Update the export line to:

```js
module.exports = { evaluateConfig, formatReport, collectInputs, defaultEasWhoami, main, OPTIONAL_FEATURES };
```

Add the CLI tail at the very END of the file:

```js
if (require.main === module) {
  const report = main({});
  if (report) {
    process.stdout.write(report + '\n');
  }
}
```

- [ ] **Step 4: Run the full detector test suite to verify it passes**

Run: `cd expo/templates/app && npx jest scripts/__tests__/config-doctor.test.js`
Expected: PASS (all three describes).

Then verify the CLI runs and stays non-fatal on the fresh template (no projectId, `com.example.*` bundle → nudge; exit 0):

Run: `cd expo/templates/app && node scripts/config-doctor.js; echo "exit=$?"`
Expected: prints a `[first-run-setup] ...` block containing `ACTION:` and ends with `exit=0`.

- [ ] **Step 5: Commit**

```bash
git add expo/templates/app/scripts/config-doctor.js expo/templates/app/scripts/__tests__/config-doctor.test.js
git commit -m "Add config-doctor IO shell, eas whoami probe, and CLI entry"
```

---

### Task 4: Wire the SessionStart hook, state gitignore, smoke test, and docs

**Files:**
- Create: `expo/templates/app/.claude/hooks/session-config-check.sh`
- Modify: `expo/templates/app/.claude/settings.json`
- Modify: `expo/templates/app/.gitignore`
- Modify: `expo/template-tests/scaffold-and-validate.ps1`
- Modify: `expo/templates/app/README.md`
- Modify: `expo/templates/app/CLAUDE.md`

**Interfaces:**
- Consumes: `scripts/config-doctor.js` CLI from Task 3.
- Produces: a `SessionStart` hook entry in `.claude/settings.json` running `bash .claude/hooks/session-config-check.sh`; the hook prints the detector's stdout (injected into session context) and always exits 0.

- [ ] **Step 1: Create the hook wrapper**

Create `expo/templates/app/.claude/hooks/session-config-check.sh`:

```bash
#!/bin/bash

if ! command -v node >/dev/null 2>&1; then
  exit 0
fi

node scripts/config-doctor.js 2>/dev/null || true
exit 0
```

- [ ] **Step 2: Wire the hook into settings.json**

Replace the contents of `expo/templates/app/.claude/settings.json` with (adds a `SessionStart` sibling to the existing `PreToolUse`):

```json
{
  "$schema": "https://json.schemastore.org/claude-code-settings.json",
  "hooks": {
    "SessionStart": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "bash .claude/hooks/session-config-check.sh"
          }
        ]
      }
    ],
    "PreToolUse": [
      {
        "matcher": "Edit|Write|MultiEdit|NotebookEdit",
        "hooks": [
          {
            "type": "command",
            "command": "bash .claude/hooks/block-main-branch.sh"
          },
          {
            "type": "command",
            "command": "bash .claude/hooks/block-merged-branch.sh"
          }
        ]
      }
    ]
  }
}
```

- [ ] **Step 3: Gitignore the local state file**

Add to `expo/templates/app/.gitignore` (append a new line after `*.p8`):

```
.claude/.setup-state.json
```

- [ ] **Step 4: Add smoke-test assertions**

In `expo/template-tests/scaffold-and-validate.ps1`, immediately AFTER the `Write-Host "Doctor must fail on a fresh scaffold..."` block (the one ending with the `submission-doctor` exit-code check) and BEFORE the `Write-Host "Partial auth config must fail..."` block, insert:

```powershell
    Write-Host "config-doctor must nudge on a fresh scaffold and exit 0..."
    $configDoctorOut = node scripts/config-doctor.js | Out-String
    if ($LASTEXITCODE -ne 0) { throw "config-doctor exited non-zero ($LASTEXITCODE) on a fresh scaffold" }
    if ($configDoctorOut -notmatch 'first-run-setup') { throw 'config-doctor did not emit a first-run-setup nudge on a fresh scaffold' }
    if ($configDoctorOut -notmatch 'ACTION:') { throw 'config-doctor nudge missing the ACTION line' }

    Write-Host "SessionStart hook + first-run-setup skill must ship in the scaffold..."
    if (-not (Test-Path (Join-Path $scaffoldDir '.claude\hooks\session-config-check.sh'))) { throw 'session-config-check.sh missing from scaffold' }
    if (-not (Test-Path (Join-Path $scaffoldDir '.claude\skills\first-run-setup\SKILL.md'))) { throw 'first-run-setup skill missing from scaffold' }
    $settingsText = Get-Content (Join-Path $scaffoldDir '.claude\settings.json') -Raw
    if ($settingsText -notmatch 'SessionStart') { throw 'settings.json missing SessionStart hook wiring' }
```

- [ ] **Step 5: Document the behavior in README and CLAUDE.md**

In `expo/templates/app/README.md`, replace the `## App Store submission` heading's preceding blank line by inserting a new section BEFORE `## App Store submission`:

```markdown
## First-run setup

At the start of each Claude Code session a hook runs `scripts/config-doctor.js`.
If the project is not yet build/deploy-ready (EAS not logged in, no EAS
project linked, placeholder bundle id, iOS credentials not provisioned) or an
optional feature (payments, auth, API URL) has no recorded decision, it prints
a nudge and Claude runs the `first-run-setup` skill to interview you and fill
the gaps. Once everything is set it stays silent.

```

In `expo/templates/app/CLAUDE.md`, append this section at the end of the file:

```markdown
## First-run config

A `SessionStart` hook runs `scripts/config-doctor.js` each session. When it
reports missing build/deploy-critical config (EAS login, `extra.eas.projectId`,
production bundle id, iOS credentials) or an undecided optional feature, run the
`first-run-setup` skill. It interviews the user, drives `eas login`/`eas init`/
`eas credentials`, writes `EXPO_PUBLIC_*` keys to `.env.local` and
`.env.production`, and records decisions in `.claude/.setup-state.json`
(gitignored). `app.config.js` edits it makes follow the normal issue→branch→PR
lifecycle above. This is separate from `submission-doctor` (store metadata).
```

- [ ] **Step 6: Verify the wiring locally**

Run: `cd expo/templates/app && bash .claude/hooks/session-config-check.sh; echo "exit=$?"`
Expected: prints the `[first-run-setup] ...` nudge and ends with `exit=0`.

Run: `node -e "JSON.parse(require('fs').readFileSync('expo/templates/app/.claude/settings.json','utf8')); console.log('settings.json valid')"`
Expected: `settings.json valid`.

- [ ] **Step 7: Commit**

```bash
git add expo/templates/app/.claude/hooks/session-config-check.sh expo/templates/app/.claude/settings.json expo/templates/app/.gitignore expo/template-tests/scaffold-and-validate.ps1 expo/templates/app/README.md expo/templates/app/CLAUDE.md
git commit -m "Wire SessionStart config-doctor hook, gitignore setup state, smoke test, and docs"
```

---

### Task 5: The `first-run-setup` interview skill

**Files:**
- Create: `expo/templates/app/.claude/skills/first-run-setup/SKILL.md`

**Interfaces:**
- Consumes: the `scripts/config-doctor.js` CLI (to read state and confirm silence) and the `.claude/.setup-state.json` schema from Global Constraints.
- Produces: a skill document Claude follows to run the interview and write `app.config.js`, `.env.local`, `.env.production`, and `.claude/.setup-state.json`.

- [ ] **Step 1: Create the skill file**

Create `expo/templates/app/.claude/skills/first-run-setup/SKILL.md`:

```markdown
---
name: first-run-setup
description: Use at session start when config-doctor reports the project is not build/deploy-ready, or when the user asks to set up the project - interviews the user, gets EAS logged in / project linked / iOS credentials provisioned and a real bundle id, offers optional payments/auth/API config, and records decisions so the session-start nudge goes quiet
---

# First-run setup (EAS build/deploy readiness + optional features)

Run this when the SessionStart config check nudges you, or when the user asks
to "set up the project" / "make it build-ready". Goal: clear every critical
item so `eas build` / `eas submit` run without hitting a setup wall, then
record optional-feature decisions.

## Read current state first

Run `node scripts/config-doctor.js` and read its report. Work only the items
it flags. Re-run it at the end - success is when it prints nothing.

## Tier 1 - critical (do these first, in order)

1. **EAS login.** Run `eas whoami`. If not logged in, ask the user to run
   `! eas login`, then re-run `eas whoami` to confirm.
2. **EAS project link.** If `app.config.js` has no `extra.eas.projectId`, run
   `eas init` (or ask the user to run `! eas init` if it needs interactive
   auth). Commit the `app.config.js` change through the CLAUDE.md dev
   lifecycle (issue -> feat branch -> PR), never directly on main.
3. **Production bundle identifier.** If the bundle id still contains
   `com.example.`, ask the user for their real reverse-DNS id (e.g.
   `com.acme.myapp`) and set it in `app.config.js`. Both the production and
   `.dev` values derive from the same string in the template. Commit via the
   lifecycle.
4. **iOS credentials.** Confirm Apple Developer membership + App Store Connect
   access (SUBMISSION.md Stage 0). If missing, tell the user this is a hard
   prerequisite and stop this step - do not loop. Otherwise run
   `! eas credentials` (interactive; needs Apple login) and provision the iOS
   distribution certificate + provisioning profile for the production bundle
   id. Record it in state (below) only after it succeeds.

## Tier 2 - optional features (offer each once)

For each of payments, auth, and API base URL that config-doctor lists as
awaiting a decision, ask the user: enable now or skip for now?

- **Payments (RevenueCat):** if enabling, get the public iOS API key and write
  `EXPO_PUBLIC_REVENUECAT_IOS_API_KEY` to BOTH `.env.local` and
  `.env.production` (create the files if missing). Full store wiring lives in
  the revenuecat-setup skill; this only captures the key.
- **Auth (Auth0):** if enabling, get the domain and client id and write
  `EXPO_PUBLIC_AUTH0_DOMAIN` and `EXPO_PUBLIC_AUTH0_CLIENT_ID` to both env
  files. Auth also requires `EXPO_PUBLIC_ACCOUNT_DELETE_URL` (Apple 5.1.1(v))
  and the `expo-apple-authentication` plugin (already in app.config.js) -
  capture the delete URL too, or record it as the next blocker. Deep Auth0
  tenant setup lives in the auth-setup skill.
- **API base URL:** if enabling, write `EXPO_PUBLIC_API_BASE_URL` to both env
  files.

If the user skips a feature, record it as `deferred` so it is not offered
again.

## Record state

Write `.claude/.setup-state.json` (gitignored):

    {
      "version": 1,
      "iosCredentials": "provisioned",
      "features": { "payments": "enabled", "auth": "deferred", "api": "deferred" }
    }

- Set `iosCredentials` to `"provisioned"` only after Tier 1 step 4 succeeds;
  omit the key until then.
- Set each feature to `"enabled"` or `"deferred"` per the user's choice. A
  feature whose env keys are already set counts as enabled even without an
  entry.

## Confirm

Re-run `node scripts/config-doctor.js`. If it prints nothing, the project is
build/deploy-ready and every optional decision is recorded. If it still
nudges, resolve the remaining flagged items.
```

- [ ] **Step 2: Verify the skill is well-formed and detected end-to-end**

Run: `node -e "const s=require('fs').readFileSync('expo/templates/app/.claude/skills/first-run-setup/SKILL.md','utf8'); if(!/^---[\s\S]*name: first-run-setup[\s\S]*---/.test(s)) throw new Error('bad frontmatter'); console.log('SKILL.md frontmatter ok')"`
Expected: `SKILL.md frontmatter ok`.

- [ ] **Step 3: Commit**

```bash
git add expo/templates/app/.claude/skills/first-run-setup/SKILL.md
git commit -m "Add first-run-setup interview skill for Expo template"
```

---

### Task 6: Full template smoke test

**Files:** none (validation only).

- [ ] **Step 1: Run the template smoke test end-to-end**

This scaffolds a fresh app, installs deps, and runs the full guardrail set plus the new config-doctor assertions from Task 4.

Run (PowerShell): `pwsh -File expo/template-tests/scaffold-and-validate.ps1`
Expected: ends with `Expo template validation passed.` and `Done.` No `throw`. In particular the new `config-doctor must nudge on a fresh scaffold and exit 0` and `SessionStart hook + first-run-setup skill must ship` lines must pass.

- [ ] **Step 2: Run the detector unit suite once more in isolation**

Run: `cd expo/templates/app && npx jest scripts/__tests__/config-doctor.test.js`
Expected: PASS.

- [ ] **Step 3: Commit (only if the smoke test required any fix)**

If Steps 1–2 surfaced a fix, commit it:

```bash
git add -A
git commit -m "Fix issues surfaced by the template smoke test"
```

Otherwise no commit is needed — this task is pure verification.
