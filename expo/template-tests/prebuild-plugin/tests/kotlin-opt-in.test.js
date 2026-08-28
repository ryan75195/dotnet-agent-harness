const assert = require('assert');
const {
  createPristinePrebuildFixture,
  readGeneratedFile,
  removeFixture,
  runPrebuildEvaluation
} = require('../lib/harness');

const PROJECT_BUILD_GRADLE = 'android/build.gradle';
const UNSTABLE_REACT_NATIVE_API_OPT_IN =
  '-opt-in=com.facebook.react.common.annotations.UnstableReactNativeAPI';

module.exports = {
  name: 'Kotlin_opt_in_for_UnstableReactNativeAPI_is_present_after_a_clean_prebuild',
  async run() {
    const fixtureRoot = createPristinePrebuildFixture();
    try {
      await runPrebuildEvaluation(fixtureRoot);
      const projectBuildGradle = readGeneratedFile(fixtureRoot, PROJECT_BUILD_GRADLE);
      const optIns = projectBuildGradle.split(UNSTABLE_REACT_NATIVE_API_OPT_IN).length - 1;
      assert.strictEqual(
        optIns,
        1,
        `expected the ${UNSTABLE_REACT_NATIVE_API_OPT_IN} compiler argument exactly once in build.gradle, got ${optIns}`
      );
      assert.ok(
        projectBuildGradle.includes('KotlinCompile'),
        'build.gradle must apply the opt-in to Kotlin compilation tasks'
      );
    } finally {
      removeFixture(fixtureRoot);
    }
  }
};
