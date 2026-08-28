'use strict';

const fs = require('fs');
const path = require('path');
const { createFixture, cleanupFixture } = require('../harness/build-fixture.js');
const { runPrebuild } = require('../harness/run-prebuild.js');

module.exports = {
  name: 'Gradle wrapper pinned to 8.14.3 after prebuild',
  async run() {
    const fixtureRoot = createFixture('gradle-wrapper-pin');
    try {
      await runPrebuild(fixtureRoot);
      const wrapper = fs.readFileSync(
        path.join(fixtureRoot, 'android', 'gradle', 'wrapper', 'gradle-wrapper.properties'),
        'utf8'
      );
      const distributionLines = wrapper
        .split('\n')
        .filter((line) => line.trim().startsWith('distributionUrl'));
      if (distributionLines.length !== 1) {
        throw new Error(
          `expected exactly one distributionUrl line in the generated gradle-wrapper.properties, found ${distributionLines.length}`
        );
      }
      if (!/gradle-8\.14\.3-bin\.zip/.test(distributionLines[0])) {
        throw new Error(
          `expected the Gradle wrapper pinned to gradle-8.14.3-bin.zip, found "${distributionLines[0].trim()}"`
        );
      }
    } finally {
      cleanupFixture(fixtureRoot);
    }
  }
};
