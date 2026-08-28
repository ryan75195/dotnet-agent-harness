'use strict';

const fs = require('fs');
const path = require('path');
const { createFixture, cleanupFixture } = require('../harness/build-fixture.js');
const { runPrebuild } = require('../harness/run-prebuild.js');

module.exports = {
  name: 'Cleartext traffic permitted on the application element after prebuild',
  async run() {
    const fixtureRoot = createFixture('cleartext-traffic');
    try {
      await runPrebuild(fixtureRoot);
      const manifestPath = path.join(fixtureRoot, 'android', 'app', 'src', 'main', 'AndroidManifest.xml');
      const manifest = fs.readFileSync(manifestPath, 'utf8');
      const applicationCount = (manifest.match(/<application[\s>]/g) || []).length;
      if (applicationCount !== 1) {
        throw new Error(
          `expected a single <application> element in the generated AndroidManifest.xml, found ${applicationCount}`
        );
      }
      if (/android:usesCleartextTraffic="true"/.test(manifest)) {
        return;
      }
      const scopedReference = manifest.match(/android:networkSecurityConfig="([^"]+)"/);
      if (!scopedReference) {
        throw new Error(
          'expected the generated <application> element to permit cleartext traffic either via android:usesCleartextTraffic="true" or via an android:networkSecurityConfig resource'
        );
      }
      const resourceRelativePath = scopedReference[1].replace(/^@xml\//, 'res/xml/') + '.xml';
      const securityConfig = fs.readFileSync(
        path.join(fixtureRoot, 'android', 'app', 'src', 'main', resourceRelativePath),
        'utf8'
      );
      if (!/cleartextTrafficPermitted="true"/.test(securityConfig)) {
        throw new Error(
          `the generated network security config ${resourceRelativePath} must permit cleartext traffic for the hosts that need it`
        );
      }
    } finally {
      cleanupFixture(fixtureRoot);
    }
  }
};
