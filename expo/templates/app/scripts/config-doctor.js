const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

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

function parseEnvFile(filePath) {
  if (!fs.existsSync(filePath)) {
    return {};
  }
  let contents;
  try {
    contents = fs.readFileSync(filePath, 'utf8');
  } catch (error) {
    return {};
  }
  const out = {};
  for (const rawLine of contents.split('\n')) {
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
  const previousEnv = process.env.NODE_ENV;
  process.env.NODE_ENV = 'development';
  let resolved;
  try {
    resolved = require.resolve(configPath);
    delete require.cache[resolved];
    const mod = require(resolved);
    const expo = (mod && mod.expo) || {};
    const projectId = (((expo.extra || {}).eas) || {}).projectId || null;
    const rawBundle = ((expo.ios || {}).bundleIdentifier) || null;
    const bundleId = rawBundle ? rawBundle.replace(/\.dev$/, '') : null;
    return { projectId, bundleId };
  } catch (error) {
    return { projectId: null, bundleId: null };
  } finally {
    if (previousEnv === undefined) {
      delete process.env.NODE_ENV;
    } else {
      process.env.NODE_ENV = previousEnv;
    }
    if (resolved) {
      delete require.cache[resolved];
    }
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

module.exports = { evaluateConfig, formatReport, collectInputs, defaultEasWhoami, main, OPTIONAL_FEATURES };

if (require.main === module) {
  const report = main({});
  if (report) {
    process.stdout.write(report + '\n');
  }
}
