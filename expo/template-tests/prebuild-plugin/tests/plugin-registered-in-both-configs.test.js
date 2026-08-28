'use strict';

const fs = require('fs');
const path = require('path');
const { createFixture, cleanupFixture, TEMPLATE_ROOT } = require('../harness/build-fixture.js');
const { loadConfig } = require('../harness/run-prebuild.js');

const PLUGIN_REFERENCE = './plugins/with-tv-sideload-patches';

function pluginEntryPresent(plugins) {
  return plugins.some((entry) => (Array.isArray(entry) ? entry[0] : entry) === PLUGIN_REFERENCE);
}

module.exports = {
  name: 'Plugin wired into both dev and production configs',
  async run() {
    const pluginPath = path.join(TEMPLATE_ROOT, 'plugins', 'with-tv-sideload-patches.js');
    if (!fs.existsSync(pluginPath)) {
      throw new Error('expo/templates/tv-app/plugins/with-tv-sideload-patches.js does not exist');
    }
    const pluginModule = require(pluginPath);
    if (typeof pluginModule !== 'function') {
      throw new Error('with-tv-sideload-patches.js must export a config plugin function');
    }
    const devFixture = createFixture('registration-dev');
    try {
      const devConfig = await loadConfig(devFixture);
      if (!pluginEntryPresent(devConfig.plugins || [])) {
        throw new Error('the dev plugin list in app.config.js does not wire with-tv-sideload-patches');
      }
    } finally {
      cleanupFixture(devFixture);
    }
    const productionFixture = createFixture('registration-production');
    try {
      const productionConfig = await loadConfig(productionFixture, { NODE_ENV: 'production' });
      if (!pluginEntryPresent(productionConfig.plugins || [])) {
        throw new Error(
          'the production plugin list in app.config.js does not wire with-tv-sideload-patches; production sideload builds would lose the patches'
        );
      }
    } finally {
      cleanupFixture(productionFixture);
    }
  }
};
