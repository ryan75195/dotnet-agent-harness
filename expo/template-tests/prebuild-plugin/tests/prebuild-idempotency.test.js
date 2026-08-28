const assert = require('assert');
const {
  createPristinePrebuildFixture,
  readGeneratedFile,
  removeFixture,
  runPrebuildEvaluation
} = require('../lib/harness');

const GENERATED_NATIVE_FILES = [
  'android/gradle/wrapper/gradle-wrapper.properties',
  'android/build.gradle',
  'android/app/src/main/AndroidManifest.xml'
];

const UNSTABLE_REACT_NATIVE_API_OPT_IN =
  '-opt-in=com.facebook.react.common.annotations.UnstableReactNativeAPI';

function snapshotGeneratedFiles(fixtureRoot) {
  return Object.fromEntries(
    GENERATED_NATIVE_FILES.map((relativePath) => [
      relativePath,
      readGeneratedFile(fixtureRoot, relativePath)
    ])
  );
}

module.exports = {
  name: 'Second_prebuild_over_a_prebuilt_project_matches_the_first_prebuild',
  async run() {
    const firstFixtureRoot = createPristinePrebuildFixture();
    try {
      await runPrebuildEvaluation(firstFixtureRoot);
      const firstPrebuild = snapshotGeneratedFiles(firstFixtureRoot);

      const firstOptIns = firstPrebuild['android/build.gradle'].split(UNSTABLE_REACT_NATIVE_API_OPT_IN).length - 1;
      assert.strictEqual(
        firstOptIns,
        1,
        `expected exactly one Kotlin opt-in after the first prebuild, got ${firstOptIns}`
      );

      const secondFixtureRoot = createPristinePrebuildFixture();
      try {
        await runPrebuildEvaluation(secondFixtureRoot);
        await runPrebuildEvaluation(secondFixtureRoot);
        const secondPrebuild = snapshotGeneratedFiles(secondFixtureRoot);
        for (const relativePath of GENERATED_NATIVE_FILES) {
          assert.strictEqual(
            secondPrebuild[relativePath],
            firstPrebuild[relativePath],
            `${relativePath} differs between the first and the second prebuild`
          );
        }
      } finally {
        removeFixture(secondFixtureRoot);
      }
    } finally {
      removeFixture(firstFixtureRoot);
    }
  }
};
