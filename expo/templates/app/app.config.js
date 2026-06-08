if (process.env.NODE_ENV === 'production' && !process.env.EAS_BUILD) {
  require('dotenv').config({ path: '.env.production' });
}

const packageJson = require('./package.json');
const isProduction = process.env.NODE_ENV === 'production';

const REQUIRED_PRODUCTION_ENV_VARS = ['EXPO_PUBLIC_REVENUECAT_IOS_API_KEY'];

if (isProduction) {
  const missing = REQUIRED_PRODUCTION_ENV_VARS.filter((name) => !process.env[name]);
  if (missing.length > 0) {
    throw new Error(`Missing required production env vars: ${missing.join(', ')}`);
  }
}

const bundleId = isProduction ? 'com.example.apptemplate' : 'com.example.apptemplate.dev';

module.exports = {
  expo: {
    name: isProduction ? 'AppTemplate' : 'AppTemplate Dev',
    slug: 'app-template',
    version: packageJson.version,
    orientation: 'portrait',
    userInterfaceStyle: 'automatic',
    assetBundlePatterns: ['**/*'],
    ios: {
      supportsTablet: false,
      bundleIdentifier: bundleId,
      infoPlist: {
        ITSAppUsesNonExemptEncryption: false
      }
    },
    android: {
      package: bundleId
    },
    plugins: isProduction ? [] : ['expo-dev-client'],
    scheme: 'apptemplate',
    extra: {}
  }
};
