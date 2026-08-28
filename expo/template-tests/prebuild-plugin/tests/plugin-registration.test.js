const assert = require('assert');
const fs = require('fs');
const {
  PLUGIN_REFERENCE,
  createPristinePrebuildFixture,
  fixturePluginsList,
  removeFixture,
  runPrebuildEvaluation,
  templateFile
} = require('../lib/harness');

module.exports = {
  name: 'Sideload_plugin_is_registered_for_dev_and_production_prebuilds',
  async run() {
    const pluginFile = templateFile(`${PLUGIN_REFERENCE}.js`);
    assert.ok(
      fs.existsSync(pluginFile),
      `the template must ship the sideload plugin at ${PLUGIN_REFERENCE}.js`
    );

    for (const mode of ['development', 'production']) {
      const fixtureRoot = createPristinePrebuildFixture();
      try {
        const references = fixturePluginsList(fixtureRoot, mode);
        assert.ok(
          references.includes(PLUGIN_REFERENCE),
          `app.config.js must register ${PLUGIN_REFERENCE} in the ${mode} plugin list`
        );
        await runPrebuildEvaluation(fixtureRoot, mode);
      } finally {
        removeFixture(fixtureRoot);
      }
    }
  }
};
