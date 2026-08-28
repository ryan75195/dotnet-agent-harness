'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');

const CONTRACT_ROOT = path.resolve(__dirname, '..');
const REPO_ROOT = path.resolve(CONTRACT_ROOT, '..', '..', '..');
const TEMPLATE_ROOT = path.join(REPO_ROOT, 'expo', 'templates', 'tv-app');

const THIRD_PARTY_PLUGIN_STUBS = ['expo-router', 'expo-apple-authentication', 'expo-dev-client'];
const EXPO_MODULE_LINKS = ['@expo/config', '@expo/config-plugins', '@react-native-tvos/config-tv'];

function copyTreeSync(source, target) {
  fs.mkdirSync(target, { recursive: true });
  for (const entry of fs.readdirSync(source, { withFileTypes: true })) {
    const sourcePath = path.join(source, entry.name);
    const targetPath = path.join(target, entry.name);
    if (entry.isDirectory()) {
      copyTreeSync(sourcePath, targetPath);
    } else {
      fs.copyFileSync(sourcePath, targetPath);
    }
  }
}

function writeDotenvStub(fixtureRoot) {
  const dotenvRoot = path.join(fixtureRoot, 'node_modules', 'dotenv');
  fs.mkdirSync(dotenvRoot, { recursive: true });
  fs.writeFileSync(
    path.join(dotenvRoot, 'package.json'),
    JSON.stringify({ name: 'dotenv', version: '0.0.0-stub', main: 'index.js' }, null, 2) + '\n'
  );
  fs.writeFileSync(path.join(dotenvRoot, 'index.js'), 'exports.config = () => ({ parsed: {} });\n');
}

function writePluginStubs(fixtureRoot, moduleNames) {
  for (const moduleName of moduleNames) {
    const moduleRoot = path.join(fixtureRoot, 'node_modules', ...moduleName.split('/'));
    fs.mkdirSync(moduleRoot, { recursive: true });
    fs.writeFileSync(
      path.join(moduleRoot, 'package.json'),
      JSON.stringify({ name: moduleName, version: '0.0.0-stub', main: 'index.js' }, null, 2) + '\n'
    );
    fs.writeFileSync(path.join(moduleRoot, 'index.js'), 'module.exports = (config) => config;\n');
  }
}

function writeExpoModuleLinks(fixtureRoot, moduleNames) {
  for (const moduleName of moduleNames) {
    const linkPath = path.join(fixtureRoot, 'node_modules', ...moduleName.split('/'));
    const target = path.join(CONTRACT_ROOT, 'node_modules', ...moduleName.split('/'));
    fs.mkdirSync(path.dirname(linkPath), { recursive: true });
    fs.symlinkSync(target, linkPath, 'dir');
  }
}

function createFixture(tag) {
  const fixtureRoot = fs.mkdtempSync(path.join(os.tmpdir(), `prebuild-plugin-fixture-${tag}-`));
  fs.copyFileSync(path.join(TEMPLATE_ROOT, 'app.config.js'), path.join(fixtureRoot, 'app.config.js'));
  fs.copyFileSync(path.join(TEMPLATE_ROOT, 'package.json'), path.join(fixtureRoot, 'package.json'));
  fs.mkdirSync(path.join(fixtureRoot, 'plugins'), { recursive: true });
  fs.copyFileSync(
    path.join(TEMPLATE_ROOT, 'plugins', 'with-tv-sideload-patches.js'),
    path.join(fixtureRoot, 'plugins', 'with-tv-sideload-patches.js')
  );
  copyTreeSync(path.join(CONTRACT_ROOT, 'harness', 'fixture-src'), fixtureRoot);
  fs.mkdirSync(path.join(fixtureRoot, 'node_modules'), { recursive: true });
  writeDotenvStub(fixtureRoot);
  writePluginStubs(fixtureRoot, THIRD_PARTY_PLUGIN_STUBS);
  writeExpoModuleLinks(fixtureRoot, EXPO_MODULE_LINKS);
  return fixtureRoot;
}

function cleanupFixture(fixtureRoot) {
  fs.rmSync(fixtureRoot, { recursive: true, force: true });
}

module.exports = { createFixture, cleanupFixture, CONTRACT_ROOT, TEMPLATE_ROOT, REPO_ROOT };
