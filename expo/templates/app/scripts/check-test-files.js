const fs = require('fs');
const path = require('path');

const ROOTS = ['src/lib', 'src/features'];

function collectSourceFiles(dir) {
  if (!fs.existsSync(dir)) {
    return [];
  }
  const files = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory() && entry.name !== '__tests__') {
      files.push(...collectSourceFiles(full));
    } else if (entry.isFile() && /\.(ts|tsx)$/.test(entry.name) && !/\.test\./.test(entry.name)) {
      files.push(full);
    }
  }
  return files;
}

function hasTestFile(sourceFile) {
  const dir = path.dirname(sourceFile);
  const base = path.basename(sourceFile).replace(/\.(ts|tsx)$/, '');
  return ['ts', 'tsx'].some((ext) =>
    fs.existsSync(path.join(dir, '__tests__', `${base}.test.${ext}`))
  );
}

const missing = ROOTS.flatMap(collectSourceFiles).filter((file) => !hasTestFile(file));

if (missing.length > 0) {
  console.error('Every module in src/lib and src/features needs a __tests__/<name>.test.ts(x) file. Missing:');
  for (const file of missing) {
    console.error(`  ${file}`);
  }
  process.exit(1);
}
console.log('check-test-files: ok');
