const fs = require('fs');
const path = require('path');

process.env.NODE_ENV = 'production';
process.env.EXPO_PUBLIC_REVENUECAT_IOS_API_KEY = process.env.EXPO_PUBLIC_REVENUECAT_IOS_API_KEY || 'doctor-stub';

const checks = [];

function check(name, fn) {
  try {
    const result = fn();
    checks.push({ name, ok: result === true, detail: result === true ? '' : result });
  } catch (error) {
    checks.push({ name, ok: false, detail: error.message });
  }
}

let config = null;

check('app.config.js loads with production env', () => {
  config = require(path.resolve('app.config.js')).expo;
  return true;
});

check('app icon is set and exists', () => {
  if (!config) {
    return 'app.config.js did not load';
  }
  if (!config.icon) {
    return 'expo.icon is not set';
  }
  return fs.existsSync(path.resolve(config.icon)) || `icon file ${config.icon} is missing`;
});

check('splash image is set and exists', () => {
  if (!config) {
    return 'app.config.js did not load';
  }
  const splash = config.splash && config.splash.image;
  if (!splash) {
    return 'expo.splash.image is not set';
  }
  return fs.existsSync(path.resolve(splash)) || `splash file ${splash} is missing`;
});

check('production bundle identifier is customised', () => {
  if (!config) {
    return 'app.config.js did not load';
  }
  const id = (config.ios && config.ios.bundleIdentifier) || '';
  return !id.includes('com.example.') || `bundle id ${id} still uses com.example.*`;
});

check('encryption declaration present', () => {
  if (!config) {
    return 'app.config.js did not load';
  }
  const plist = (config.ios && config.ios.infoPlist) || {};
  return plist.ITSAppUsesNonExemptEncryption !== undefined || 'ITSAppUsesNonExemptEncryption missing from infoPlist';
});

check('.env.production has the RevenueCat iOS API key', () => {
  if (!fs.existsSync('.env.production')) {
    return '.env.production is missing';
  }
  const content = fs.readFileSync('.env.production', 'utf8');
  return /EXPO_PUBLIC_REVENUECAT_IOS_API_KEY=.+/.test(content) || 'EXPO_PUBLIC_REVENUECAT_IOS_API_KEY is not set';
});

check('react-native-purchases is installed', () => {
  const pkg = JSON.parse(fs.readFileSync('package.json', 'utf8'));
  return Boolean(pkg.dependencies['react-native-purchases']) || 'react-native-purchases missing from dependencies';
});

check('eas.json has a production build profile', () => {
  const eas = JSON.parse(fs.readFileSync('eas.json', 'utf8'));
  return Boolean(eas.build && eas.build.production) || 'production build profile missing';
});

check('SUBMISSION.md exists', () => fs.existsSync('SUBMISSION.md') || 'SUBMISSION.md is missing');

for (const result of checks) {
  const mark = result.ok ? 'PASS' : 'FAIL';
  const detail = result.ok ? '' : ` — ${result.detail}`;
  console.log(`${mark}  ${result.name}${detail}`);
}

if (checks.some((result) => !result.ok)) {
  process.exit(1);
}
console.log('submission-doctor: all checks passed');
