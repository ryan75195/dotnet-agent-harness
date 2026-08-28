const assert = require('assert');
const {
  createPristinePrebuildFixture,
  readGeneratedFile,
  removeFixture,
  runPrebuildEvaluation
} = require('../lib/harness');

const GRADLE_WRAPPER_PROPERTIES = 'android/gradle/wrapper/gradle-wrapper.properties';
const PINNED_DISTRIBUTION_URL =
  'distributionUrl=https\\://services.gradle.org/distributions/gradle-8.14.3-bin.zip';

module.exports = {
  name: 'Gradle_wrapper_is_pinned_to_8_14_3_after_a_clean_prebuild',
  async run() {
    const fixtureRoot = createPristinePrebuildFixture();
    try {
      await runPrebuildEvaluation(fixtureRoot);
      const wrapperProperties = readGeneratedFile(fixtureRoot, GRADLE_WRAPPER_PROPERTIES);
      const distributionUrls = wrapperProperties
        .split('\n')
        .filter((line) => line.startsWith('distributionUrl='));
      assert.strictEqual(
        distributionUrls.length,
        1,
        `expected exactly one distributionUrl line, got ${distributionUrls.length}`
      );
      assert.strictEqual(distributionUrls[0], PINNED_DISTRIBUTION_URL);
    } finally {
      removeFixture(fixtureRoot);
    }
  }
};
