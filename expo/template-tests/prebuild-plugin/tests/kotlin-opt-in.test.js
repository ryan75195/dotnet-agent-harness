'use strict';

const fs = require('fs');
const path = require('path');
const { createFixture, cleanupFixture } = require('../harness/build-fixture.js');
const { runPrebuild } = require('../harness/run-prebuild.js');

const OPT_IN = '-opt-in=com.facebook.react.common.annotations.UnstableReactNativeAPI';

module.exports = {
  name: 'Kotlin unstable API opt-in present after prebuild',
  async run() {
    const fixtureRoot = createFixture('kotlin-opt-in');
    try {
      await runPrebuild(fixtureRoot);
      const buildGradle = fs.readFileSync(path.join(fixtureRoot, 'android', 'build.gradle'), 'utf8');
      const optInCount = (buildGradle.match(/-opt-in=com\.facebook\.react\.common\.annotations\.UnstableReactNativeAPI/g) || [])
        .length;
      if (optInCount !== 1) {
        throw new Error(
          `expected the @UnstableReactNativeAPI opt-in exactly once in the generated android/build.gradle, found ${optInCount}`
        );
      }
      if (!buildGradle.includes('KotlinCompile')) {
        throw new Error(
          'the opt-in must be applied to KotlinCompile tasks so :expo:compileReleaseKotlin honors it'
        );
      }
    } finally {
      cleanupFixture(fixtureRoot);
    }
  }
};
