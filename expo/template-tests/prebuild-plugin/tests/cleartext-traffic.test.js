const assert = require('assert');
const {
  createPristinePrebuildFixture,
  readGeneratedFile,
  removeFixture,
  runPrebuildEvaluation
} = require('../lib/harness');

const ANDROID_MANIFEST = 'android/app/src/main/AndroidManifest.xml';

function applicationElements(manifestXml) {
  return manifestXml.match(/<application\b[^>]*>/g) ?? [];
}

module.exports = {
  name: 'Cleartext_traffic_is_permitted_after_a_clean_prebuild',
  async run() {
    const fixtureRoot = createPristinePrebuildFixture();
    try {
      await runPrebuildEvaluation(fixtureRoot);
      const manifestXml = readGeneratedFile(fixtureRoot, ANDROID_MANIFEST);
      const applications = applicationElements(manifestXml);
      assert.strictEqual(
        applications.length,
        1,
        `expected exactly one <application> element, got ${applications.length}`
      );
      assert.ok(
        applications[0].includes('android:usesCleartextTraffic="true"'),
        'the <application> element must carry android:usesCleartextTraffic="true"'
      );
    } finally {
      removeFixture(fixtureRoot);
    }
  }
};
