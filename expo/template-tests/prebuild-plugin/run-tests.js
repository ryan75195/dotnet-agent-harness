'use strict';

const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const CONTRACT_ROOT = __dirname;
const TEST_FILES = [
  'gradle-wrapper-pin.test.js',
  'kotlin-opt-in.test.js',
  'cleartext-traffic.test.js',
  'prebuild-idempotency.test.js',
  'readme-manual-patches.test.js',
  'plugin-registered-in-both-configs.test.js'
];

function ensureDependencies() {
  try {
    require.resolve('@expo/config', { paths: [CONTRACT_ROOT] });
    require.resolve('@expo/config-plugins', { paths: [CONTRACT_ROOT] });
    return;
  } catch (error) {
    // Dependencies are not installed yet; fall through to npm ci.
  }
  if (!fs.existsSync(path.join(CONTRACT_ROOT, 'package-lock.json'))) {
    console.error('Missing node_modules and package-lock.json; cannot provide harness dependencies.');
    process.exit(1);
  }
  const install = spawnSync('npm', ['ci', '--no-audit', '--no-fund'], {
    cwd: CONTRACT_ROOT,
    stdio: 'inherit'
  });
  if (install.error || install.status !== 0) {
    console.error('npm ci failed while restoring the prebuild-plugin contract harness dependencies.');
    process.exit(1);
  }
}

async function main() {
  ensureDependencies();
  let failedCount = 0;
  for (const testFile of TEST_FILES) {
    const test = require(path.join(CONTRACT_ROOT, 'tests', testFile));
    try {
      await test.run();
      console.log(`Passed ${test.name}`);
    } catch (error) {
      failedCount += 1;
      console.log(`Failed ${test.name}`);
      const message = String(error && error.message ? error.message : error);
      console.log(`${message.split('\n').join('\n    ')}`);
    }
  }
  if (failedCount === 0) {
    console.log('All prebuild-plugin contract tests passed.');
  } else {
    console.log(`${failedCount} prebuild-plugin contract test(s) failed.`);
  }
  process.exit(failedCount === 0 ? 0 : 1);
}

main();
