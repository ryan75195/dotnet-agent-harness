'use strict';

const fs = require('fs');
const path = require('path');
const { REPO_ROOT } = require('../harness/build-fixture.js');

const BANNED_MANUAL_PATCH_INSTRUCTIONS = [
  {
    label: 'the instruction to re-apply the sideload patches after every fresh prebuild',
    snippet: 'must be re-applied after every fresh prebuild'
  },
  {
    label: 'the hand-edit snippet pinning the Gradle wrapper',
    snippet: 'distributionUrl=https\\://services.gradle.org/distributions/gradle-8.14.3-bin.zip'
  },
  {
    label: 'the hand-edit snippet adding the Kotlin opt-in',
    snippet: 'freeCompilerArgs.add("-opt-in=com.facebook.react.common.annotations.UnstableReactNativeAPI")'
  },
  {
    label: 'the hand-edit instruction adding the cleartext flag',
    snippet: 'Add `android:usesCleartextTraffic="true"`'
  }
];

module.exports = {
  name: 'README no longer instructs hand-applied sideload patches',
  async run() {
    const readmePath = path.join(REPO_ROOT, 'expo', 'templates', 'tv-app', 'README.md');
    const readme = fs.readFileSync(readmePath, 'utf8');
    for (const { label, snippet } of BANNED_MANUAL_PATCH_INSTRUCTIONS) {
      if (readme.includes(snippet)) {
        throw new Error(
          `${label} is still present in expo/templates/tv-app/README.md, but the prebuild plugin applies this patch automatically`
        );
      }
    }
  }
};
