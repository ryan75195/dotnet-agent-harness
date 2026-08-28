'use strict';

const fs = require('fs');
const path = require('path');
const { createFixture, cleanupFixture } = require('../harness/build-fixture.js');
const { runPrebuild } = require('../harness/run-prebuild.js');

const GENERATED_FILES = [
  path.join('android', 'gradle', 'wrapper', 'gradle-wrapper.properties'),
  path.join('android', 'build.gradle'),
  path.join('android', 'app', 'src', 'main', 'AndroidManifest.xml')
];

function snapshot(fixtureRoot) {
  const snapshot = {};
  for (const relativePath of GENERATED_FILES) {
    snapshot[relativePath] = fs.readFileSync(path.join(fixtureRoot, relativePath), 'utf8');
  }
  return snapshot;
}

function assertPatchesApplied(snapshot) {
  const wrapperLines = snapshot[GENERATED_FILES[0]]
    .split('\n')
    .filter((line) => line.trim().startsWith('distributionUrl'));
  if (wrapperLines.length !== 1 || !/gradle-8\.14\.3-bin\.zip/.test(wrapperLines[0])) {
    throw new Error(
      'the first prebuild did not pin the Gradle wrapper, so idempotency cannot be judged'
    );
  }
  if (
    !/-opt-in=com\.facebook\.react\.common\.annotations\.UnstableReactNativeAPI/.test(snapshot[GENERATED_FILES[1]])
  ) {
    throw new Error(
      'the first prebuild did not add the Kotlin opt-in, so idempotency cannot be judged'
    );
  }
  const manifest = snapshot[GENERATED_FILES[2]];
  const cleartextPermitted =
    /android:usesCleartextTraffic="true"/.test(manifest) ||
    /android:networkSecurityConfig="[^"]+"/.test(manifest);
  if (!cleartextPermitted) {
    throw new Error(
      'the first prebuild did not permit cleartext traffic, so idempotency cannot be judged'
    );
  }
}

module.exports = {
  name: 'Second prebuild over already-patched output is byte-identical to the first',
  async run() {
    const fixtureRoot = createFixture('prebuild-idempotency');
    try {
      await runPrebuild(fixtureRoot);
      const firstRun = snapshot(fixtureRoot);
      assertPatchesApplied(firstRun);
      await runPrebuild(fixtureRoot);
      const secondRun = snapshot(fixtureRoot);
      for (const relativePath of GENERATED_FILES) {
        if (firstRun[relativePath] !== secondRun[relativePath]) {
          throw new Error(
            `${relativePath} changed when prebuild ran a second time; a plugin that appends rather than sets accumulates duplicate entries on every prebuild`
          );
        }
      }
    } finally {
      cleanupFixture(fixtureRoot);
    }
  }
};
