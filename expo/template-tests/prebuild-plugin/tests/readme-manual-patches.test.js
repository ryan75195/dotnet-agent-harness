const assert = require('assert');
const fs = require('fs');
const { templateFile } = require('../lib/harness');

const MANUAL_PATCH_INSTRUCTIONS = [
  'must be re-applied after every fresh prebuild',
  'Append to `android/build.gradle`',
  '`android/gradle/wrapper/gradle-wrapper.properties` set',
  'Add `android:usesCleartextTraffic="true"` to'
];

module.exports = {
  name: 'Readme_no_longer_documents_hand_applied_sideload_patches',
  run() {
    const readme = fs.readFileSync(templateFile('README.md'), 'utf8');
    for (const instruction of MANUAL_PATCH_INSTRUCTIONS) {
      assert.ok(
        !readme.includes(instruction),
        `README still documents a hand-applied patch: ${instruction}`
      );
    }
  }
};
