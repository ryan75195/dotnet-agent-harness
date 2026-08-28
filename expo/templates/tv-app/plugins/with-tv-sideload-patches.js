const fs = require('fs');
const path = require('path');

const GRADLE_WRAPPER_PROPERTIES = path.join('android', 'gradle', 'wrapper', 'gradle-wrapper.properties');
const PINNED_DISTRIBUTION_URL = 'https\\://services.gradle.org/distributions/gradle-8.14.3-bin.zip';
const KOTLIN_OPT_IN = '-opt-in=com.facebook.react.common.annotations.UnstableReactNativeAPI';
const KOTLIN_OPT_IN_BLOCK = [
  'subprojects {',
  '  tasks.withType(org.jetbrains.kotlin.gradle.tasks.KotlinCompile).configureEach {',
  '    compilerOptions {',
  `      freeCompilerArgs.add("${KOTLIN_OPT_IN}")`,
  '    }',
  '  }',
  '}'
].join('\n');

function pinGradleWrapper(projectRoot) {
  const wrapperPropertiesPath = path.join(projectRoot, GRADLE_WRAPPER_PROPERTIES);
  if (!fs.existsSync(wrapperPropertiesPath)) {
    return;
  }
  const contents = fs.readFileSync(wrapperPropertiesPath, 'utf8');
  const pinnedLine = `distributionUrl=${PINNED_DISTRIBUTION_URL}`;
  const lines = contents.split('\n');
  const nextLines = [];
  let pinned = false;
  for (const line of lines) {
    if (/^\s*distributionUrl\s*=/.test(line)) {
      if (!pinned) {
        pinned = true;
        nextLines.push(line.endsWith('\r') ? `${pinnedLine}\r` : pinnedLine);
      }
    } else {
      nextLines.push(line);
    }
  }
  if (!pinned) {
    let insertAt = nextLines.length;
    while (insertAt > 0 && nextLines[insertAt - 1].trim() === '') {
      insertAt -= 1;
    }
    nextLines.splice(insertAt, 0, pinnedLine);
  }
  const nextContents = nextLines.join('\n');
  if (nextContents !== contents) {
    fs.writeFileSync(wrapperPropertiesPath, nextContents);
  }
}

function withPinnedGradleWrapper(config) {
  const { withDangerousMod } = require('@expo/config-plugins');
  return withDangerousMod(config, [
    'android',
    (modConfig) => {
      pinGradleWrapper(modConfig.modRequest.projectRoot);
      return modConfig;
    }
  ]);
}

function withKotlinUnstableApiOptIn(config) {
  const { withProjectBuildGradle } = require('@expo/config-plugins');
  return withProjectBuildGradle(config, (modConfig) => {
    if (modConfig.modResults.contents.includes(KOTLIN_OPT_IN)) {
      return modConfig;
    }
    const contents = modConfig.modResults.contents.replace(/\s*$/, '');
    modConfig.modResults.contents = `${contents}\n\n${KOTLIN_OPT_IN_BLOCK}\n`;
    return modConfig;
  });
}

function withCleartextTraffic(config) {
  const { withAndroidManifest } = require('@expo/config-plugins');
  return withAndroidManifest(config, (modConfig) => {
    const applications = (modConfig.modResults.manifest && modConfig.modResults.manifest.application) || [];
    for (const application of applications) {
      application.$ = application.$ || {};
      application.$['android:usesCleartextTraffic'] = 'true';
    }
    return modConfig;
  });
}

module.exports = function withTvSideloadPatches(config) {
  config = withPinnedGradleWrapper(config);
  config = withKotlinUnstableApiOptIn(config);
  config = withCleartextTraffic(config);
  return config;
};
