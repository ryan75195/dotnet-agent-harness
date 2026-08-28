'use strict';

const path = require('path');
const { getConfig } = require('@expo/config');
const { compileModsAsync } = require('@expo/config-plugins');

const TRANSIENT_ENV_KEYS = [
  'NODE_ENV',
  'EAS_BUILD',
  'EXPO_PUBLIC_AUTH0_DOMAIN',
  'EXPO_PUBLIC_AUTH0_CLIENT_ID',
  'EXPO_PUBLIC_REVENUECAT_IOS_API_KEY',
  'EXPO_PUBLIC_REVENUECAT_ANDROID_API_KEY'
];

function bustRequireCache(fixtureRoot) {
  for (const key of Object.keys(require.cache)) {
    if (key.startsWith(fixtureRoot + path.sep)) {
      delete require.cache[key];
    }
  }
}

async function withTransientEnv(env, callback) {
  const saved = new Map();
  for (const key of TRANSIENT_ENV_KEYS) {
    saved.set(key, process.env[key]);
    delete process.env[key];
  }
  for (const [key, value] of Object.entries(env)) {
    process.env[key] = value;
  }
  try {
    return await callback();
  } finally {
    for (const [key, value] of saved) {
      if (value === undefined) {
        delete process.env[key];
      } else {
        process.env[key] = value;
      }
    }
  }
}

async function loadConfig(fixtureRoot, env = {}) {
  return withTransientEnv(env, async () => {
    bustRequireCache(fixtureRoot);
    const { exp } = getConfig(fixtureRoot, { skipSDKVersionRequirement: true, isModdedConfig: true });
    return exp;
  });
}

async function runPrebuild(fixtureRoot, env = {}) {
  const exp = await loadConfig(fixtureRoot, env);
  await compileModsAsync(exp, {
    projectRoot: fixtureRoot,
    platforms: ['android'],
    assertMissingModProviders: false
  });
  return exp;
}

module.exports = { loadConfig, runPrebuild, TRANSIENT_ENV_KEYS };
