if (process.env.NODE_ENV === 'production' && !process.env.EAS_BUILD) {
  require('dotenv').config({ path: '.env.production' });
}

const packageJson = require('./package.json');
const isProduction = process.env.NODE_ENV === 'production';
const AUTH_ENV_VARS = ['EXPO_PUBLIC_AUTH0_DOMAIN', 'EXPO_PUBLIC_AUTH0_CLIENT_ID'];

function purchaseEnvVar() {
  const platform = process.env.EAS_BUILD_PLATFORM ?? process.env.EXPO_TV_PLATFORM ?? 'ios';
  return platform === 'android' ? 'EXPO_PUBLIC_REVENUECAT_ANDROID_API_KEY' : 'EXPO_PUBLIC_REVENUECAT_IOS_API_KEY';
}

function missingProductionVars() {
  const missing = [purchaseEnvVar()].filter((name) => !process.env[name]);
  const setAuthVars = AUTH_ENV_VARS.filter((name) => process.env[name]);
  if (setAuthVars.length > 0 && setAuthVars.length < AUTH_ENV_VARS.length) {
    missing.push(...AUTH_ENV_VARS.filter((name) => !process.env[name]));
  }
  return missing;
}

if (isProduction) {
  const missing = missingProductionVars();
  if (missing.length > 0) {
    throw new Error(`Missing required production env vars: ${missing.join(', ')}`);
  }
}

const bundleId = isProduction ? 'com.example.apptemplate' : 'com.example.apptemplate.dev';
const plugins = isProduction
  ? ['expo-router', 'expo-apple-authentication', ['@react-native-tvos/config-tv', { isTV: true }]]
  : ['expo-router', 'expo-dev-client', 'expo-apple-authentication', ['@react-native-tvos/config-tv', { isTV: true }]];

module.exports = {
  expo: {
    name: isProduction ? 'AppTemplate' : 'AppTemplate Dev',
    slug: 'app-template',
    version: packageJson.version,
    orientation: 'landscape',
    userInterfaceStyle: 'dark',
    assetBundlePatterns: ['**/*'],
    ios: {
      bundleIdentifier: bundleId,
      infoPlist: {
        ITSAppUsesNonExemptEncryption: false
      }
    },
    android: {
      package: bundleId
    },
    plugins,
    scheme: 'apptemplate',
    extra: {
      isTV: true
    }
  }
};
