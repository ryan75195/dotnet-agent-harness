const fs = require('fs');
const os = require('os');
const path = require('path');
const { getConfig } = require('@expo/config');
const { compileModsAsync } = require('@expo/config-plugins');

const REPO_ROOT = path.resolve(__dirname, '..', '..', '..', '..');
const TV_TEMPLATE_DIR = path.join(REPO_ROOT, 'expo', 'templates', 'tv-app');
const PLUGIN_REFERENCE = './plugins/with-tv-sideload-patches';

const PRISTINE_GRADLE_WRAPPER_PROPERTIES = [
  'distributionBase=GRADLE_USER_HOME',
  'distributionPath=wrapper/dists',
  'distributionUrl=https\\://services.gradle.org/distributions/gradle-9.0.0-bin.zip',
  'networkTimeout=10000',
  'validateDistributionUrl=true',
  'zipStoreBase=GRADLE_USER_HOME',
  'zipStorePath=wrapper/dists',
  ''
].join('\n');

const PRISTINE_GRADLE_PROPERTIES = [
  'org.gradle.jvmargs=-Xmx64m',
  'android.useAndroidX=true',
  'reactNativeArchitectures=armeabi-v7a,arm64-v8a,x86,x86_64',
  'newArchEnabled=true',
  'hermesEnabled=true',
  ''
].join('\n');

const PRISTINE_PROJECT_BUILD_GRADLE = [
  'buildscript {',
  '    ext {',
  "        buildToolsVersion = findProperty('android.buildToolsVersion') ?? '36.0.0'",
  '        minSdkVersion = 24',
  '        compileSdkVersion = 36',
  '        targetSdkVersion = 36',
  "        kotlinVersion = findProperty('android.kotlinVersion') ?? '2.1.20'",
  '    }',
  '    repositories {',
  '        google()',
  '        mavenCentral()',
  '    }',
  '    dependencies {',
  "        classpath('com.android.tools.build:gradle')",
  "        classpath('com.facebook.react:react-native-gradle-plugin')",
  "        classpath('org.jetbrains.kotlin:kotlin-gradle-plugin')",
  '    }',
  '}',
  '',
  'apply plugin: "com.facebook.react.rootproject"',
  ''
].join('\n');

const PRISTINE_ANDROID_MANIFEST = [
  '<manifest xmlns:android="http://schemas.android.com/apk/res/android" xmlns:tools="http://schemas.android.com/tools" package="com.example.tvappfixture">',
  '',
  '    <uses-permission android:name="android.permission.INTERNET"/>',
  '',
  '    <queries>',
  '        <intent>',
  '            <action android:name="android.intent.action.VIEW"/>',
  '            <category android:name="android.intent.category.BROWSABLE"/>',
  '            <data android:scheme="https"/>',
  '        </intent>',
  '    </queries>',
  '',
  '    <application',
  '      android:name=".MainApplication"',
  '      android:label="@string/app_name"',
  '      android:icon="@mipmap/ic_launcher"',
  '      android:allowBackup="false"',
  '      android:theme="@style/AppTheme"',
  '      android:supportsRtl="true">',
  '      <activity',
  '        android:name=".MainActivity"',
  '        android:launchMode="singleTask"',
  '        android:exported="true">',
  '        <intent-filter>',
  '            <action android:name="android.intent.action.MAIN"/>',
  '            <category android:name="android.intent.category.LAUNCHER"/>',
  '        </intent-filter>',
  '      </activity>',
  '    </application>',
  '</manifest>',
  ''
].join('\n');

const PRISTINE_STRINGS_XML =
  '<?xml version="1.0" encoding="UTF-8"?><resources><string name="app_name">TV App Fixture</string></resources>';

function writeInertPluginStub(nodeModules, name) {
  const [scope, packageName] = name.startsWith('@') ? name.split('/') : [null, name];
  const packageDir = scope
    ? path.join(nodeModules, scope, packageName)
    : path.join(nodeModules, packageName);
  fs.mkdirSync(packageDir, { recursive: true });
  fs.writeFileSync(
    path.join(packageDir, 'package.json'),
    JSON.stringify({ name, version: '0.0.0', main: 'index.js' })
  );
  fs.writeFileSync(
    path.join(packageDir, 'index.js'),
    'module.exports = function inertPlugin(config) { return config; };\n'
  );
}

function bustRequireCacheUnder(directory) {
  const prefixes = [directory + path.sep];
  if (fs.existsSync(directory)) {
    prefixes.push(fs.realpathSync(directory) + path.sep);
  }
  for (const resolved of Object.keys(require.cache)) {
    if (prefixes.some((prefix) => resolved.startsWith(prefix))) {
      delete require.cache[resolved];
    }
  }
}

function fixturePluginsList(fixtureRoot, mode) {
  const previousNodeEnv = process.env.NODE_ENV;
  const appConfigPath = path.join(fixtureRoot, 'app.config.js');
  try {
    if (mode === 'production') {
      process.env.NODE_ENV = 'production';
    } else {
      delete process.env.NODE_ENV;
    }
    bustRequireCacheUnder(fixtureRoot);
    const raw = require(appConfigPath);
    return raw.expo.plugins.map((entry) => (typeof entry === 'string' ? entry : entry[0]));
  } finally {
    if (previousNodeEnv === undefined) {
      delete process.env.NODE_ENV;
    } else {
      process.env.NODE_ENV = previousNodeEnv;
    }
  }
}

function createPristinePrebuildFixture() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'tv-prebuild-fixture-'));
  const android = path.join(root, 'android');
  for (const relativeDir of ['android/gradle/wrapper', 'android/app/src/main/res/values']) {
    fs.mkdirSync(path.join(root, relativeDir), { recursive: true });
  }
  fs.writeFileSync(
    path.join(android, 'gradle', 'wrapper', 'gradle-wrapper.properties'),
    PRISTINE_GRADLE_WRAPPER_PROPERTIES
  );
  fs.writeFileSync(path.join(android, 'gradle.properties'), PRISTINE_GRADLE_PROPERTIES);
  fs.writeFileSync(path.join(android, 'build.gradle'), PRISTINE_PROJECT_BUILD_GRADLE);
  fs.writeFileSync(
    path.join(android, 'app', 'src', 'main', 'AndroidManifest.xml'),
    PRISTINE_ANDROID_MANIFEST
  );
  fs.writeFileSync(
    path.join(android, 'app', 'src', 'main', 'res', 'values', 'strings.xml'),
    PRISTINE_STRINGS_XML
  );

  fs.copyFileSync(path.join(TV_TEMPLATE_DIR, 'app.config.js'), path.join(root, 'app.config.js'));
  fs.copyFileSync(path.join(TV_TEMPLATE_DIR, 'package.json'), path.join(root, 'package.json'));
  fs.cpSync(path.join(TV_TEMPLATE_DIR, 'plugins'), path.join(root, 'plugins'), {
    recursive: true
  });

  const nodeModules = path.join(root, 'node_modules');
  const dotenvDir = path.join(nodeModules, 'dotenv');
  fs.mkdirSync(dotenvDir, { recursive: true });
  fs.writeFileSync(
    path.join(dotenvDir, 'package.json'),
    JSON.stringify({ name: 'dotenv', version: '0.0.0', main: 'index.js' })
  );
  fs.writeFileSync(path.join(dotenvDir, 'index.js'), 'module.exports = { config: () => ({ parsed: {} }) };\n');

  const thirdPartyReferences = new Set();
  for (const mode of ['development', 'production']) {
    for (const reference of fixturePluginsList(root, mode)) {
      if (!reference.startsWith('.')) {
        thirdPartyReferences.add(reference);
      }
    }
  }
  for (const reference of thirdPartyReferences) {
    writeInertPluginStub(nodeModules, reference);
  }
  return root;
}

async function runPrebuildEvaluation(fixtureRoot, mode = 'development') {
  const previousNodeEnv = process.env.NODE_ENV;
  try {
    if (mode === 'production') {
      process.env.NODE_ENV = 'production';
    } else {
      delete process.env.NODE_ENV;
    }
    bustRequireCacheUnder(fixtureRoot);
    const { exp } = getConfig(fixtureRoot, {
      skipSDKVersionRequirement: true,
      isModdedConfig: true
    });
    return await compileModsAsync(exp, {
      projectRoot: fixtureRoot,
      platforms: ['android'],
      assertMissingModProviders: true
    });
  } finally {
    if (previousNodeEnv === undefined) {
      delete process.env.NODE_ENV;
    } else {
      process.env.NODE_ENV = previousNodeEnv;
    }
  }
}

function removeFixture(fixtureRoot) {
  fs.rmSync(fixtureRoot, { recursive: true, force: true });
}

function readGeneratedFile(fixtureRoot, relativePath) {
  return fs.readFileSync(path.join(fixtureRoot, relativePath), 'utf8');
}

function templateFile(relativePath) {
  return path.join(TV_TEMPLATE_DIR, relativePath);
}

module.exports = {
  PLUGIN_REFERENCE,
  createPristinePrebuildFixture,
  fixturePluginsList,
  readGeneratedFile,
  removeFixture,
  runPrebuildEvaluation,
  templateFile
};
