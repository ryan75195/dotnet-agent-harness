const fs = require('fs');
const path = require('path');
const { Module } = require('module');

process.env.NODE_PATH = path.join(__dirname, 'node_modules');
Module._initPaths();

const testsDir = path.join(__dirname, 'tests');

function summary(failures) {
  if (failures.length === 0) {
    console.log(`All ${fs.readdirSync(testsDir).length} prebuild plugin contract tests passed.`);
  } else {
    console.log(`${failures.length} of ${fs.readdirSync(testsDir).length} prebuild plugin contract tests failed.`);
  }
}

async function main() {
  const testFiles = fs
    .readdirSync(testsDir)
    .filter((file) => file.endsWith('.test.js'))
    .sort();
  const failures = [];
  for (const file of testFiles) {
    const test = require(path.join(testsDir, file));
    try {
      await test.run();
      console.log(`Passed ${test.name}`);
    } catch (error) {
      failures.push(test.name);
      const message = (error && error.message ? error.message : String(error)).split('\n')[0];
      console.log(`Failed ${test.name}`);
      console.log(`  ${message}`);
    }
  }
  summary(failures);
  if (failures.length > 0) {
    process.exitCode = 1;
  }
}

main().catch((error) => {
  console.log(`Failed run-tests.js`);
  console.log(`  ${(error && error.message) || error}`);
  process.exitCode = 1;
});
