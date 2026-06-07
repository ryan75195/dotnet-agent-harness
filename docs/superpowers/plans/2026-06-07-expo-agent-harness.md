# Expo Agent Harness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repurpose this repo into a multi-language agent-harness monorepo (`dotnet/` + `expo/`) and build an Expo template with full error-severity guardrails plus a CLAUDE.md-orchestrated iOS App Store submission layer.

**Architecture:** The dotnet templates move unchanged under `dotnet/`. A new `expo/templates/app/` directory holds a checked-in Expo SDK 55 project (RevenueCat baked in) scaffolded by copy-and-rename scripts. Guardrails are enforced by the toolchain (tsc strict, ESLint custom plugin, dependency-cruiser, jest coverage, git hooks); submission is a staged workflow driven by `SUBMISSION.md` state + four Claude skills, routed by the template's CLAUDE.md.

**Tech Stack:** Expo SDK 55 / React Native 0.83 / React 19.2, TypeScript ~5.9, ESLint 9 flat config + typescript-eslint 8, dependency-cruiser 16, jest 29 + jest-expo + @testing-library/react-native 12, react-native-purchases 9, PowerShell scaffolding scripts, bash git hooks.

**Spec:** `docs/superpowers/specs/2026-06-07-expo-agent-harness-design.md`

**Working notes for the implementer:**
- This harness repo has no pre-commit hooks itself; commit directly to `main` after each task (matches existing repo history).
- All `npm`/`npx` commands for template development run inside `expo/templates/app/` (it has its own `package.json`; `node_modules` there is gitignored).
- The template uses placeholder identity: app name `AppTemplate`, slug `app-template`, scheme `apptemplate`, bundle id `com.example.apptemplate`. `expo/new-app.ps1` string-replaces these.
- **No comments in any template source file** — the template's own no-comments rule lints `src/`, and CLAUDE.md style applies everywhere else. The plan's code blocks below contain zero comments; keep it that way.

---

## File structure (end state)

```
dotnet-agent-harness/
  README.md                                  ← rewritten index (Task 16)
  .github/workflows/template-ci.yml          ← + expo job (Task 15)
  dotnet/                                    ← moved (Task 1)
    README.md                                ← old root README, paths fixed
    templates/cli/  templates/etl-api/
    template-tests/scaffold-and-build.ps1
  expo/
    new-app.ps1                              ← scaffolder (Task 9)
    template-tests/scaffold-and-validate.ps1 ← smoke test (Task 15)
    templates/app/
      package.json  tsconfig.json  babel.config.js  app.config.js  eas.json
      index.ts  App.tsx  .nvmrc  .gitignore  .env.example
      jest.config.js  jest.setup.js
      eslint.config.js
      eslint-rules/index.js
      eslint-rules/no-comments.js
      eslint-rules/one-component-per-file.js
      eslint-rules/__tests__/no-comments.test.js
      eslint-rules/__tests__/one-component-per-file.test.js
      .dependency-cruiser.cjs
      scripts/check-test-files.js
      scripts/submission-doctor.js
      src/app/HomeScreen.tsx
      src/app/PaywallScreen.tsx
      src/app/__tests__/PaywallScreen.test.tsx
      src/components/PackageRow.tsx
      src/features/.gitkeep
      src/lib/purchases/initPurchases.ts
      src/lib/purchases/useSubscription.ts
      src/lib/purchases/__tests__/initPurchases.test.ts
      src/lib/purchases/__tests__/useSubscription.test.tsx
      .githooks/pre-commit  .githooks/reference-transaction
      .claude/settings.json
      .claude/hooks/block-main-branch.sh  .claude/hooks/block-merged-branch.sh
      .claude/skills/submission-doctor/SKILL.md
      .claude/skills/asc-setup/SKILL.md
      .claude/skills/revenuecat-setup/SKILL.md
      .claude/skills/build-and-submit/SKILL.md
      CLAUDE.md
      SUBMISSION.md
      setup.ps1
      README.md
```

Layer rules: `src/lib` imports nothing from `src/app|features|components`; `src/components` imports nothing from `src/app|features`; `src/features/<x>` never imports `src/features/<y>`.

---

### Task 1: Restructure repo into `dotnet/` + `expo/`

**Files:**
- Move: `templates/` → `dotnet/templates/`, `template-tests/` → `dotnet/template-tests/`, `README.md` → `dotnet/README.md`
- Modify: `dotnet/README.md` (path references)
- Create: `expo/` (empty for now, created implicitly by later tasks)

- [ ] **Step 1: Move the dotnet pieces**

```powershell
New-Item -ItemType Directory dotnet
git mv templates dotnet/templates
git mv template-tests dotnet/template-tests
git mv README.md dotnet/README.md
```

- [ ] **Step 2: Fix paths in `dotnet/README.md`**

Three edits (exact old → new):

1. Install section:
```
OLD: dotnet new install .\dotnet-agent-harness
NEW: dotnet new install .\dotnet-agent-harness\dotnet
```
2. Local validation in the Development section (two occurrences):
```
OLD: .\template-tests\scaffold-and-build.ps1 cli
NEW: .\dotnet\template-tests\scaffold-and-build.ps1 cli
OLD: .\template-tests\scaffold-and-build.ps1 etl-api
NEW: .\dotnet\template-tests\scaffold-and-build.ps1 etl-api
```
3. Repo layout block — replace the whole fenced tree with:
```
dotnet-agent-harness/
  README.md                                     ← multi-language index
  .github/workflows/template-ci.yml             ← CI for all templates
  dotnet/
    README.md                                   ← this file
    template-tests/scaffold-and-build.ps1
    templates/
      cli/
      etl-api/
  expo/
    ...                                         ← Expo app template (see root README)
```

No change needed in `dotnet/template-tests/scaffold-and-build.ps1` — it computes `$repoRoot` as the parent of its own directory, which is now `dotnet/`, and `dotnet new install` from there still finds both templates. No change needed in the CI workflow — `dotnet new install .` scans recursively.

- [ ] **Step 3: Verify the dotnet smoke test still passes**

Run: `.\dotnet\template-tests\scaffold-and-build.ps1 cli`
Expected: `Smoke test passed.` then `Done.`

- [ ] **Step 4: Commit**

```powershell
git add -A; git commit -m "Restructure repo into dotnet/ language folder"
```

---

### Task 2: Expo template base configuration

**Files:**
- Create: `expo/templates/app/package.json`, `tsconfig.json`, `babel.config.js`, `app.config.js`, `eas.json`, `index.ts`, `App.tsx`, `.nvmrc`, `.gitignore`, `.env.example`, `src/features/.gitkeep`

- [ ] **Step 1: Write `expo/templates/app/package.json`**

```json
{
  "name": "app-template",
  "version": "1.0.0",
  "main": "index.ts",
  "scripts": {
    "start": "expo start",
    "typecheck": "tsc --noEmit",
    "lint": "eslint . --max-warnings 0",
    "depcruise": "depcruise src --config .dependency-cruiser.cjs",
    "check-test-files": "node scripts/check-test-files.js",
    "test": "jest",
    "doctor": "node scripts/submission-doctor.js",
    "verify": "npm run typecheck && npm run lint && npm run depcruise && npm run check-test-files && npm run test -- --coverage"
  },
  "dependencies": {
    "dotenv": "^17.0.1",
    "expo": "^55.0.5",
    "expo-dev-client": "~55.0.13",
    "expo-status-bar": "~55.0.4",
    "react": "19.2.0",
    "react-native": "0.83.2",
    "react-native-purchases": "^9.6.6"
  },
  "devDependencies": {
    "@testing-library/react-native": "^12.0.0",
    "@types/jest": "^29.5.14",
    "@types/react": "~19.1.10",
    "babel-preset-expo": "~55.0.8",
    "dependency-cruiser": "^16.0.0",
    "eslint": "^9.0.0",
    "jest": "^29.7.0",
    "jest-expo": "~55.0.0",
    "react-test-renderer": "19.2.0",
    "typescript": "~5.9.2",
    "typescript-eslint": "^8.0.0"
  },
  "private": true
}
```

- [ ] **Step 2: Write `tsconfig.json`, `babel.config.js`, `.nvmrc`**

`expo/templates/app/tsconfig.json`:
```json
{
  "extends": "expo/tsconfig.base",
  "compilerOptions": {
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "noImplicitOverride": true
  },
  "exclude": ["node_modules", "eslint-rules", "scripts", "coverage"]
}
```

`expo/templates/app/babel.config.js`:
```js
module.exports = function (api) {
  api.cache(true);
  return { presets: ['babel-preset-expo'] };
};
```

`expo/templates/app/.nvmrc`:
```
22
```

- [ ] **Step 3: Write `app.config.js`**

```js
if (process.env.NODE_ENV === 'production' && !process.env.EAS_BUILD) {
  require('dotenv').config({ path: '.env.production' });
}

const packageJson = require('./package.json');
const isProduction = process.env.NODE_ENV === 'production';

const REQUIRED_PRODUCTION_ENV_VARS = ['EXPO_PUBLIC_REVENUECAT_IOS_API_KEY'];

if (isProduction) {
  const missing = REQUIRED_PRODUCTION_ENV_VARS.filter((name) => !process.env[name]);
  if (missing.length > 0) {
    throw new Error(`Missing required production env vars: ${missing.join(', ')}`);
  }
}

const bundleId = isProduction ? 'com.example.apptemplate' : 'com.example.apptemplate.dev';

module.exports = {
  expo: {
    name: isProduction ? 'AppTemplate' : 'AppTemplate Dev',
    slug: 'app-template',
    version: packageJson.version,
    orientation: 'portrait',
    userInterfaceStyle: 'automatic',
    assetBundlePatterns: ['**/*'],
    ios: {
      supportsTablet: false,
      bundleIdentifier: bundleId,
      infoPlist: {
        ITSAppUsesNonExemptEncryption: false
      }
    },
    android: {
      package: bundleId
    },
    plugins: isProduction ? [] : ['expo-dev-client'],
    scheme: 'apptemplate',
    extra: {}
  }
};
```

Note: no `icon`/`splash` yet — the template ships no binary assets; `submission-doctor` flags this as a Stage 1 blocker so every real app adds its own before submission. `expo-dev-client` is excluded from production builds because it injects `NSAllowsArbitraryLoads` (App Store ATS rejection — learned on journal-app).

- [ ] **Step 4: Write `eas.json`**

```json
{
  "cli": {
    "version": ">= 16.10.0",
    "appVersionSource": "remote"
  },
  "build": {
    "development": {
      "developmentClient": true,
      "distribution": "internal"
    },
    "preview": {
      "distribution": "internal",
      "ios": {
        "resourceClass": "m-medium"
      }
    },
    "production": {
      "autoIncrement": true,
      "ios": {
        "resourceClass": "m-medium",
        "bundler": "metro"
      },
      "env": {
        "NODE_ENV": "production"
      }
    }
  },
  "submit": {
    "production": {}
  }
}
```

- [ ] **Step 5: Write `index.ts`, minimal `App.tsx`, `.gitignore`, `.env.example`, `src/features/.gitkeep`**

`index.ts`:
```ts
import { registerRootComponent } from 'expo';

import App from './App';

registerRootComponent(App);
```

`App.tsx` (minimal for now; final version in Task 7):
```tsx
import { StatusBar } from 'expo-status-bar';
import { Text, View } from 'react-native';

export default function App() {
  return (
    <View>
      <Text>AppTemplate</Text>
      <StatusBar style="auto" />
    </View>
  );
}
```

`.gitignore`:
```
node_modules/
.expo/
coverage/
ios/
android/
.env.local
.env.production
*.p8
```

`.env.example`:
```
EXPO_PUBLIC_REVENUECAT_IOS_API_KEY=
```

`src/features/.gitkeep`: empty file.

- [ ] **Step 6: Install and reconcile expo-managed versions**

```powershell
Push-Location expo\templates\app
npm install
npx expo install --fix
```
Expected: install succeeds; `expo install --fix` may rewrite expo-managed dep versions (`expo-*`, `jest-expo`, `react-native`, `@types/react`) in `package.json` — keep whatever it writes.

- [ ] **Step 7: Verify typecheck passes**

Run (in `expo/templates/app`): `npm run typecheck`
Expected: exits 0, no output errors.

- [ ] **Step 8: Commit**

```powershell
Pop-Location
git add expo; git commit -m "Add expo template base configuration"
```

Note: commit `package-lock.json` (it is deliberately absent from the template's `.gitignore`) so scaffolded apps resolve identical versions. Scaffolds use `npm install`, not `npm ci`, because renaming breaks the lockfile's `name` field match — npm tolerates this on `install`.

---

### Task 3: Jest wiring

**Files:**
- Create: `expo/templates/app/jest.config.js`, `jest.setup.js`

- [ ] **Step 1: Write `jest.config.js`**

```js
module.exports = {
  preset: 'jest-expo',
  setupFiles: ['./jest.setup.js'],
  testMatch: ['**/__tests__/**/*.test.[jt]s?(x)'],
  collectCoverageFrom: [
    'src/lib/**/*.{ts,tsx}',
    'src/features/**/*.{ts,tsx}',
    '!**/__tests__/**'
  ],
  coverageThreshold: {
    global: {
      lines: 80,
      branches: 80
    }
  }
};
```

- [ ] **Step 2: Write `jest.setup.js`**

```js
jest.mock('react-native-purchases', () => ({
  __esModule: true,
  default: {
    configure: jest.fn(),
    getCustomerInfo: jest.fn().mockResolvedValue({ entitlements: { active: {} } }),
    addCustomerInfoUpdateListener: jest.fn(),
    getOfferings: jest.fn().mockResolvedValue({ current: null }),
    purchasePackage: jest.fn()
  }
}));
```

- [ ] **Step 3: Verify jest runs**

Run (in `expo/templates/app`): `npx jest --passWithNoTests`
Expected: `No tests found, exiting with code 0` (tests arrive in Tasks 4, 6, 7).

- [ ] **Step 4: Commit**

```powershell
git add expo; git commit -m "Add jest wiring to expo template"
```

---

### Task 4: ESLint guardrails (custom plugin, TDD)

**Files:**
- Create: `expo/templates/app/eslint-rules/no-comments.js`, `eslint-rules/one-component-per-file.js`, `eslint-rules/index.js`, `eslint-rules/__tests__/no-comments.test.js`, `eslint-rules/__tests__/one-component-per-file.test.js`, `eslint.config.js`

- [ ] **Step 1: Write the failing rule tests**

`eslint-rules/__tests__/no-comments.test.js`:
```js
const { RuleTester } = require('eslint');
const rule = require('../no-comments');

const ruleTester = new RuleTester({
  languageOptions: { ecmaVersion: 2022, sourceType: 'module' }
});

ruleTester.run('no-comments', rule, {
  valid: [
    { code: 'const a = 1;' },
    { code: 'export function f() { return 1; }' }
  ],
  invalid: [
    { code: 'const a = 1; // note', errors: [{ messageId: 'noComments' }] },
    { code: '/* block */ const a = 1;', errors: [{ messageId: 'noComments' }] },
    {
      code: '/** doc */\nconst a = 1; // two',
      errors: [{ messageId: 'noComments' }, { messageId: 'noComments' }]
    }
  ]
});
```

`eslint-rules/__tests__/one-component-per-file.test.js`:
```js
const { RuleTester } = require('eslint');
const rule = require('../one-component-per-file');

const ruleTester = new RuleTester({
  languageOptions: {
    ecmaVersion: 2022,
    sourceType: 'module',
    parserOptions: { ecmaFeatures: { jsx: true } }
  }
});

ruleTester.run('one-component-per-file', rule, {
  valid: [
    { code: 'export function Foo() { return null; }' },
    { code: 'export function Foo() { return null; }\nfunction helper() { return 1; }' },
    { code: 'export default function App() { return null; }' },
    { code: 'export const useThing = () => 1;\nexport function Foo() { return null; }' }
  ],
  invalid: [
    {
      code: 'export function Foo() { return null; }\nexport function Bar() { return null; }',
      errors: [{ messageId: 'tooMany' }]
    },
    {
      code: 'export const Foo = () => null;\nexport const Bar = () => null;',
      errors: [{ messageId: 'tooMany' }]
    },
    {
      code: 'export default function App() { return null; }\nexport function Other() { return null; }',
      errors: [{ messageId: 'tooMany' }]
    }
  ]
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run (in `expo/templates/app`): `npx jest eslint-rules`
Expected: FAIL — `Cannot find module '../no-comments'`.

- [ ] **Step 3: Implement the rules**

`eslint-rules/no-comments.js`:
```js
module.exports = {
  meta: {
    type: 'problem',
    schema: [],
    messages: {
      noComments: 'Comments are banned — extract intent into function, variable, or type names (see CLAUDE.md).'
    }
  },
  create(context) {
    return {
      Program() {
        for (const comment of context.sourceCode.getAllComments()) {
          if (comment.type === 'Shebang') {
            continue;
          }
          context.report({ loc: comment.loc, messageId: 'noComments' });
        }
      }
    };
  }
};
```

`eslint-rules/one-component-per-file.js`:
```js
function exportedName(declaration) {
  if (!declaration) {
    return null;
  }
  if (declaration.type === 'FunctionDeclaration' && declaration.id) {
    return declaration.id.name;
  }
  if (declaration.type === 'VariableDeclaration') {
    const declarator = declaration.declarations[0];
    const isFunction =
      declarator &&
      declarator.id.type === 'Identifier' &&
      declarator.init &&
      ['ArrowFunctionExpression', 'FunctionExpression'].includes(declarator.init.type);
    return isFunction ? declarator.id.name : null;
  }
  return null;
}

module.exports = {
  meta: {
    type: 'problem',
    schema: [],
    messages: {
      tooMany: 'Only one exported component per file — move {{name}} to its own file.'
    }
  },
  create(context) {
    const components = [];
    function record(node) {
      const name = exportedName(node.declaration);
      if (name && /^[A-Z]/.test(name)) {
        components.push({ name, node });
      }
    }
    return {
      ExportNamedDeclaration: record,
      ExportDefaultDeclaration: record,
      'Program:exit'() {
        for (const extra of components.slice(1)) {
          context.report({ node: extra.node, messageId: 'tooMany', data: { name: extra.name } });
        }
      }
    };
  }
};
```

`eslint-rules/index.js`:
```js
module.exports = {
  rules: {
    'no-comments': require('./no-comments'),
    'one-component-per-file': require('./one-component-per-file')
  }
};
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `npx jest eslint-rules`
Expected: PASS, 2 suites.

- [ ] **Step 5: Write `eslint.config.js`**

```js
const tseslint = require('typescript-eslint');
const localRules = require('./eslint-rules');

module.exports = tseslint.config(
  {
    ignores: [
      'node_modules/**',
      '.expo/**',
      'coverage/**',
      'eslint-rules/**',
      'scripts/**',
      '**/*.config.js',
      'app.config.js',
      'jest.setup.js',
      '.dependency-cruiser.cjs'
    ]
  },
  ...tseslint.configs.recommended,
  {
    files: ['**/*.ts', '**/*.tsx'],
    linterOptions: { noInlineConfig: true },
    plugins: { local: localRules },
    rules: {
      'local/no-comments': 'error',
      'local/one-component-per-file': 'error',
      '@typescript-eslint/no-explicit-any': 'error',
      'max-lines-per-function': ['error', { max: 60, skipBlankLines: true }],
      'max-params': ['error', 4],
      'max-lines': ['error', { max: 300, skipBlankLines: true }]
    }
  },
  {
    files: ['**/__tests__/**'],
    rules: {
      'max-lines-per-function': 'off'
    }
  }
);
```

`noInlineConfig: true` is the load-bearing line: it makes `eslint-disable` comments inert, so the agent cannot opt out of any rule (and the comment itself is flagged by `local/no-comments` anyway).

- [ ] **Step 6: Verify the template lints clean and the rule fires**

Run: `npm run lint`
Expected: exit 0.

Then seed a violation and confirm it fires:
```powershell
Set-Content src\smoke.ts "// seeded`nexport const smoke = 1;"
npx eslint src/smoke.ts
```
Expected: 1 error `local/no-comments`. Then `Remove-Item src\smoke.ts`.

- [ ] **Step 7: Commit**

```powershell
git add expo; git commit -m "Add ESLint guardrails with local no-comments and one-component rules"
```

---

### Task 5: dependency-cruiser + test-file existence check

**Files:**
- Create: `expo/templates/app/.dependency-cruiser.cjs`, `scripts/check-test-files.js`

- [ ] **Step 1: Write `.dependency-cruiser.cjs`**

```js
module.exports = {
  forbidden: [
    {
      name: 'no-circular',
      severity: 'error',
      from: {},
      to: { circular: true }
    },
    {
      name: 'features-no-cross-import',
      severity: 'error',
      from: { path: '^src/features/([^/]+)/' },
      to: { path: '^src/features/', pathNot: '^src/features/$1/' }
    },
    {
      name: 'lib-no-upward-import',
      severity: 'error',
      from: { path: '^src/lib/' },
      to: { path: '^src/(app|features|components)/' }
    },
    {
      name: 'components-no-feature-import',
      severity: 'error',
      from: { path: '^src/components/' },
      to: { path: '^src/(app|features)/' }
    }
  ],
  options: {
    doNotFollow: { path: 'node_modules' },
    tsConfig: { fileName: 'tsconfig.json' }
  }
};
```

- [ ] **Step 2: Write `scripts/check-test-files.js`**

```js
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
```

- [ ] **Step 3: Verify both pass on the current (empty-lib) template**

Run (in `expo/templates/app`): `npm run depcruise` then `npm run check-test-files`
Expected: depcruise reports `no dependency violations found`; check-test-files prints `check-test-files: ok`.

- [ ] **Step 4: Verify check-test-files fails when a module lacks a test**

```powershell
Set-Content src\lib\orphan.ts "export const orphan = 1;"
npm run check-test-files
```
Expected: exit 1, lists `src\lib\orphan.ts`. Then `Remove-Item src\lib\orphan.ts`.

- [ ] **Step 5: Commit**

```powershell
git add expo; git commit -m "Add dependency-cruiser rules and test-file existence check"
```

---

### Task 6: Purchases lib (TDD)

**Files:**
- Create: `expo/templates/app/src/lib/purchases/__tests__/initPurchases.test.ts`, `__tests__/useSubscription.test.tsx`, `initPurchases.ts`, `useSubscription.ts`

- [ ] **Step 1: Write the failing tests**

`src/lib/purchases/__tests__/initPurchases.test.ts`:
```ts
import Purchases from 'react-native-purchases';

import { initPurchases } from '../initPurchases';

describe('initPurchases', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    delete process.env.EXPO_PUBLIC_REVENUECAT_IOS_API_KEY;
  });

  test('configures RevenueCat when the api key is set', () => {
    process.env.EXPO_PUBLIC_REVENUECAT_IOS_API_KEY = 'rc-test-key';
    initPurchases();
    expect(Purchases.configure).toHaveBeenCalledWith({ apiKey: 'rc-test-key' });
  });

  test('does nothing when the api key is missing', () => {
    initPurchases();
    expect(Purchases.configure).not.toHaveBeenCalled();
  });
});
```

`src/lib/purchases/__tests__/useSubscription.test.tsx`:
```tsx
import { renderHook, waitFor } from '@testing-library/react-native';
import Purchases, { CustomerInfo } from 'react-native-purchases';

import { PREMIUM_ENTITLEMENT, useSubscription } from '../useSubscription';

const mockedPurchases = Purchases as jest.Mocked<typeof Purchases>;

function customerInfoWith(activeEntitlements: Record<string, unknown>): CustomerInfo {
  return { entitlements: { active: activeEntitlements } } as unknown as CustomerInfo;
}

describe('useSubscription', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  test('reports subscribed when the premium entitlement is active', async () => {
    mockedPurchases.getCustomerInfo.mockResolvedValue(
      customerInfoWith({ [PREMIUM_ENTITLEMENT]: { identifier: PREMIUM_ENTITLEMENT } })
    );
    const { result } = renderHook(() => useSubscription());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isSubscribed).toBe(true);
  });

  test('reports not subscribed when no entitlements are active', async () => {
    mockedPurchases.getCustomerInfo.mockResolvedValue(customerInfoWith({}));
    const { result } = renderHook(() => useSubscription());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isSubscribed).toBe(false);
  });

  test('reports not subscribed when the customer info lookup fails', async () => {
    mockedPurchases.getCustomerInfo.mockRejectedValue(new Error('network'));
    const { result } = renderHook(() => useSubscription());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isSubscribed).toBe(false);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npx jest src/lib/purchases`
Expected: FAIL — cannot find `../initPurchases` / `../useSubscription`.

- [ ] **Step 3: Implement**

`src/lib/purchases/initPurchases.ts`:
```ts
import { Platform } from 'react-native';
import Purchases from 'react-native-purchases';

export function initPurchases(): void {
  const apiKey = process.env.EXPO_PUBLIC_REVENUECAT_IOS_API_KEY ?? '';
  if (Platform.OS !== 'ios' || apiKey === '') {
    return;
  }
  Purchases.configure({ apiKey });
}
```

`src/lib/purchases/useSubscription.ts`:
```ts
import { useEffect, useState } from 'react';
import Purchases, { CustomerInfo } from 'react-native-purchases';

export const PREMIUM_ENTITLEMENT = 'premium';

export type SubscriptionState = {
  isSubscribed: boolean;
  isLoading: boolean;
};

function hasPremium(info: CustomerInfo): boolean {
  return info.entitlements.active[PREMIUM_ENTITLEMENT] !== undefined;
}

export function useSubscription(): SubscriptionState {
  const [state, setState] = useState<SubscriptionState>({ isSubscribed: false, isLoading: true });

  useEffect(() => {
    let mounted = true;
    Purchases.getCustomerInfo()
      .then((info) => {
        if (mounted) {
          setState({ isSubscribed: hasPremium(info), isLoading: false });
        }
      })
      .catch(() => {
        if (mounted) {
          setState({ isSubscribed: false, isLoading: false });
        }
      });
    Purchases.addCustomerInfoUpdateListener((info) => {
      if (mounted) {
        setState({ isSubscribed: hasPremium(info), isLoading: false });
      }
    });
    return () => {
      mounted = false;
    };
  }, []);

  return state;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `npx jest src/lib/purchases`
Expected: PASS, 5 tests.

- [ ] **Step 5: Run the full guardrail set**

Run: `npm run verify`
Expected: typecheck, lint, depcruise, check-test-files all pass; jest passes with coverage ≥ 80% on `src/lib` (the only collected code so far).

- [ ] **Step 6: Commit**

```powershell
git add expo; git commit -m "Add RevenueCat purchases lib with subscription hook"
```

---

### Task 7: Screens + final App.tsx (TDD)

**Files:**
- Create: `expo/templates/app/src/app/__tests__/PaywallScreen.test.tsx`, `src/app/PaywallScreen.tsx`, `src/app/HomeScreen.tsx`, `src/components/PackageRow.tsx`
- Modify: `App.tsx`

- [ ] **Step 1: Write the failing test**

`src/app/__tests__/PaywallScreen.test.tsx`:
```tsx
import { render, screen, waitFor } from '@testing-library/react-native';
import Purchases from 'react-native-purchases';

import { PaywallScreen } from '../PaywallScreen';

const mockedPurchases = Purchases as jest.Mocked<typeof Purchases>;

type Offerings = Awaited<ReturnType<typeof Purchases.getOfferings>>;

function offeringsWith(packages: Array<{ identifier: string; title: string; priceString: string }>): Offerings {
  return {
    current: {
      availablePackages: packages.map((pkg) => ({
        identifier: pkg.identifier,
        product: { title: pkg.title, priceString: pkg.priceString }
      }))
    }
  } as unknown as Offerings;
}

describe('PaywallScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  test('renders packages from the current offering', async () => {
    mockedPurchases.getOfferings.mockResolvedValue(
      offeringsWith([{ identifier: 'monthly', title: 'Plus Monthly', priceString: '£4.99' }])
    );
    render(<PaywallScreen onClose={jest.fn()} />);
    await waitFor(() => expect(screen.getByText('Plus Monthly')).toBeTruthy());
    expect(screen.getByText('£4.99')).toBeTruthy();
  });

  test('renders the close action when offerings fail to load', async () => {
    mockedPurchases.getOfferings.mockRejectedValue(new Error('network'));
    render(<PaywallScreen onClose={jest.fn()} />);
    await waitFor(() => expect(screen.getByText('Not now')).toBeTruthy());
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx jest src/app`
Expected: FAIL — cannot find `../PaywallScreen`.

- [ ] **Step 3: Implement the components**

`src/components/PackageRow.tsx`:
```tsx
import { useState } from 'react';
import { Pressable, StyleSheet, Text } from 'react-native';
import Purchases, { PurchasesPackage } from 'react-native-purchases';

type PackageRowProps = {
  pkg: PurchasesPackage;
  onPurchased: () => void;
};

export function PackageRow({ pkg, onPurchased }: PackageRowProps) {
  const [isPurchasing, setIsPurchasing] = useState(false);

  const handlePress = async () => {
    setIsPurchasing(true);
    try {
      await Purchases.purchasePackage(pkg);
      onPurchased();
    } catch {
      setIsPurchasing(false);
    }
  };

  return (
    <Pressable onPress={handlePress} disabled={isPurchasing} style={styles.row} accessibilityRole="button">
      <Text style={styles.title}>{pkg.product.title}</Text>
      <Text style={styles.price}>{pkg.product.priceString}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  row: { padding: 16, borderRadius: 12, backgroundColor: '#1f2430', marginVertical: 6 },
  title: { color: '#ffffff', fontSize: 16, fontWeight: '600' },
  price: { color: '#9be29b', fontSize: 14, marginTop: 4 }
});
```

`src/app/PaywallScreen.tsx`:
```tsx
import { useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';
import Purchases, { PurchasesPackage } from 'react-native-purchases';

import { PackageRow } from '../components/PackageRow';

type PaywallScreenProps = {
  onClose: () => void;
};

export function PaywallScreen({ onClose }: PaywallScreenProps) {
  const [packages, setPackages] = useState<PurchasesPackage[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    Purchases.getOfferings()
      .then((offerings) => {
        setPackages(offerings.current?.availablePackages ?? []);
        setIsLoading(false);
      })
      .catch(() => setIsLoading(false));
  }, []);

  if (isLoading) {
    return (
      <View style={styles.container}>
        <ActivityIndicator />
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Go Premium</Text>
      {packages.map((pkg) => (
        <PackageRow key={pkg.identifier} pkg={pkg} onPurchased={onClose} />
      ))}
      <Pressable onPress={onClose} accessibilityRole="button">
        <Text style={styles.close}>Not now</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, justifyContent: 'center', padding: 24, backgroundColor: '#10131a' },
  title: { color: '#ffffff', fontSize: 28, fontWeight: '700', marginBottom: 16 },
  close: { color: '#8a93a6', fontSize: 16, textAlign: 'center', marginTop: 24 }
});
```

`src/app/HomeScreen.tsx`:
```tsx
import { Pressable, StyleSheet, Text, View } from 'react-native';

type HomeScreenProps = {
  isSubscribed: boolean;
  onUpgradePress: () => void;
};

export function HomeScreen({ isSubscribed, onUpgradePress }: HomeScreenProps) {
  return (
    <View style={styles.container}>
      <Text style={styles.title}>AppTemplate</Text>
      <Text style={styles.subtitle}>{isSubscribed ? 'Premium active' : 'Free plan'}</Text>
      {!isSubscribed && (
        <Pressable onPress={onUpgradePress} accessibilityRole="button" style={styles.upgrade}>
          <Text style={styles.upgradeText}>Upgrade</Text>
        </Pressable>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#10131a' },
  title: { color: '#ffffff', fontSize: 32, fontWeight: '700' },
  subtitle: { color: '#8a93a6', fontSize: 16, marginTop: 8 },
  upgrade: { marginTop: 24, paddingHorizontal: 24, paddingVertical: 12, borderRadius: 12, backgroundColor: '#4c6ef5' },
  upgradeText: { color: '#ffffff', fontSize: 16, fontWeight: '600' }
});
```

`App.tsx` (replaces the Task 2 minimal version):
```tsx
import { useState } from 'react';
import { StatusBar } from 'expo-status-bar';

import { HomeScreen } from './src/app/HomeScreen';
import { PaywallScreen } from './src/app/PaywallScreen';
import { initPurchases } from './src/lib/purchases/initPurchases';
import { useSubscription } from './src/lib/purchases/useSubscription';

initPurchases();

export default function App() {
  const [showPaywall, setShowPaywall] = useState(false);
  const { isSubscribed } = useSubscription();

  if (showPaywall && !isSubscribed) {
    return <PaywallScreen onClose={() => setShowPaywall(false)} />;
  }

  return (
    <>
      <HomeScreen isSubscribed={isSubscribed} onUpgradePress={() => setShowPaywall(true)} />
      <StatusBar style="light" />
    </>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `npx jest src/app`
Expected: PASS, 2 tests.

- [ ] **Step 5: Run full verify**

Run: `npm run verify`
Expected: all gates pass (coverage is still collected from `src/lib` + `src/features` only, both covered).

- [ ] **Step 6: Commit**

```powershell
git add expo; git commit -m "Add paywall and home screens wired to subscription state"
```

---

### Task 8: Git hooks + Claude hooks

**Files:**
- Create: `expo/templates/app/.githooks/pre-commit`
- Copy: `dotnet/templates/cli/.githooks/reference-transaction` → `expo/templates/app/.githooks/reference-transaction` (unchanged — it is language-agnostic: branch-name format + `gh issue view`)
- Copy: `dotnet/templates/cli/.claude/hooks/block-main-branch.sh` and `block-merged-branch.sh` → `expo/templates/app/.claude/hooks/` (unchanged)
- Copy: `dotnet/templates/cli/.claude/settings.json` → `expo/templates/app/.claude/settings.json` (unchanged)

- [ ] **Step 1: Write `.githooks/pre-commit`**

```bash
#!/bin/bash

BRANCH=$(git branch --show-current 2>/dev/null)
if [[ "$BRANCH" == "main" || "$BRANCH" == "master" ]]; then
  echo "BLOCKED: Cannot commit directly to $BRANCH. Create a feature branch first."
  exit 1
fi

REPO_ROOT=$(git rev-parse --show-toplevel 2>/dev/null)
cd "$REPO_ROOT" || exit 1

if [ -x "$REPO_ROOT/.claude/hooks/block-merged-branch.sh" ]; then
  "$REPO_ROOT/.claude/hooks/block-merged-branch.sh" || exit $?
fi

echo "=== Pre-Commit Checks ==="

echo "--- Typecheck ---"
npx tsc --noEmit || { echo "TYPECHECK FAILED. Fix all type errors before committing."; exit 1; }

echo "--- Lint ---"
npx eslint . --max-warnings 0 || { echo "LINT FAILED. local/no-comments and friends fire at error severity."; exit 1; }

echo "--- Dependency rules ---"
npx depcruise src --config .dependency-cruiser.cjs || { echo "DEPENDENCY RULES FAILED. See .dependency-cruiser.cjs."; exit 1; }

echo "--- Test files ---"
node scripts/check-test-files.js || exit 1

echo "--- Tests + coverage ---"
npx jest --coverage --silent || { echo "TESTS FAILED (or coverage below threshold)."; exit 1; }

echo "=== All checks passed ==="
exit 0
```

- [ ] **Step 2: Copy the shared hooks**

```powershell
New-Item -ItemType Directory expo\templates\app\.claude\hooks -Force
Copy-Item dotnet\templates\cli\.githooks\reference-transaction expo\templates\app\.githooks\
Copy-Item dotnet\templates\cli\.claude\hooks\block-main-branch.sh expo\templates\app\.claude\hooks\
Copy-Item dotnet\templates\cli\.claude\hooks\block-merged-branch.sh expo\templates\app\.claude\hooks\
Copy-Item dotnet\templates\cli\.claude\settings.json expo\templates\app\.claude\
```

- [ ] **Step 3: Verify the pre-commit hook passes inside the template**

Run (in `expo/templates/app`, git-bash via the Bash tool):
```bash
cd expo/templates/app && bash .githooks/pre-commit
```
Expected: it blocks immediately with `BLOCKED: Cannot commit directly to main` — correct behavior (the harness repo is on `main`; the hook is for scaffolded apps). To verify the check pipeline itself, run the body manually: `npx tsc --noEmit && npx eslint . --max-warnings 0 && npx depcruise src --config .dependency-cruiser.cjs && node scripts/check-test-files.js && npx jest --coverage --silent` — expected exit 0.

- [ ] **Step 4: Commit**

```powershell
git add expo; git commit -m "Add git hooks and Claude hooks to expo template"
```

---

### Task 9: Scaffolding scripts

**Files:**
- Create: `expo/new-app.ps1`, `expo/templates/app/setup.ps1`

- [ ] **Step 1: Write `expo/new-app.ps1`**

```powershell
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string]$Name,
    [string]$Destination = (Join-Path (Get-Location) $Name),
    [string]$BundleId = "com.example.$($Name.ToLower())"
)

$ErrorActionPreference = 'Stop'

$templateDir = Join-Path $PSScriptRoot 'templates\app'
if (Test-Path $Destination) {
    throw "Destination $Destination already exists."
}

Write-Host "Copying template to $Destination..."
robocopy $templateDir $Destination /E /XD node_modules .expo coverage .git /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) {
    throw "robocopy failed with exit code $LASTEXITCODE"
}
$global:LASTEXITCODE = 0

$slug = ($Name -creplace '(?<=[a-z0-9])(?=[A-Z])', '-').ToLower()
$lower = $Name.ToLower()
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

Write-Host "Renaming placeholders (name=$Name, slug=$slug, bundle=$BundleId)..."
Get-ChildItem $Destination -Recurse -File | ForEach-Object {
    $content = [IO.File]::ReadAllText($_.FullName)
    $updated = $content.
        Replace('com.example.apptemplate', $BundleId).
        Replace('AppTemplate', $Name).
        Replace('app-template', $slug).
        Replace('apptemplate', $lower)
    if ($updated -ne $content) {
        [IO.File]::WriteAllText($_.FullName, $updated, $utf8NoBom)
    }
}

Write-Host ""
Write-Host "Scaffolded $Name. Next steps:"
Write-Host "  cd $Destination"
Write-Host "  .\setup.ps1"
```

Replacement order matters: `com.example.apptemplate` must be replaced before the bare `apptemplate` token.

- [ ] **Step 2: Write `expo/templates/app/setup.ps1`**

```powershell
$ErrorActionPreference = 'Stop'

function Invoke-Git {
    & git @Args
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Args -join ' ') failed with exit code $LASTEXITCODE"
    }
}

$gitEmail = git config user.email 2>$null
$gitName = git config user.name 2>$null
if (-not $gitEmail -or -not $gitName) {
    Write-Host "ERROR: git identity is not configured." -ForegroundColor Red
    Write-Host "Configure it once globally, then re-run setup.ps1:"
    Write-Host "  git config --global user.email 'your@email.com'"
    Write-Host "  git config --global user.name 'Your Name'"
    exit 1
}

Write-Host "Installing dependencies..."
npm install
if ($LASTEXITCODE -ne 0) {
    throw "npm install failed"
}

Write-Host "Initializing git repo..."
Invoke-Git init -q -b main
Invoke-Git add .

Write-Host "Activating .githooks..."
Invoke-Git config core.hooksPath .githooks

Write-Host "Creating initial commit..."
Invoke-Git commit -q --no-verify -m "Initial scaffold from expo app template"

Write-Host ""
Write-Host "Done. Next steps:"
Write-Host "  1. npm run verify"
Write-Host "  2. gh repo create"
Write-Host "  3. Read SUBMISSION.md, then ask Claude to run the submission-doctor skill"
```

- [ ] **Step 3: Verify scaffolding round-trips**

```powershell
.\expo\new-app.ps1 -Name ScaffoldCheck -Destination $env:TEMP\scaffold-check
Select-String -Path $env:TEMP\scaffold-check\app.config.js -Pattern 'ScaffoldCheck|com.example.scaffoldcheck' | Measure-Object
Select-String -Path $env:TEMP\scaffold-check\app.config.js -Pattern 'AppTemplate|apptemplate' | Measure-Object
Remove-Item -Recurse -Force $env:TEMP\scaffold-check
```
Expected: first Measure-Object count ≥ 2; second count = 0.

- [ ] **Step 4: Commit**

```powershell
git add expo; git commit -m "Add expo scaffolding scripts"
```

---

### Task 10: SUBMISSION.md + submission-doctor

**Files:**
- Create: `expo/templates/app/SUBMISSION.md`, `scripts/submission-doctor.js`, `.claude/skills/submission-doctor/SKILL.md`

- [ ] **Step 1: Write `SUBMISSION.md`**

```markdown
# SUBMISSION.md — iOS App Store submission state

Single source of truth for submission progress. Skills read this file to find the
current stage and update it (check boxes, record values) after every completed step.
Stages are strictly ordered — never start stage N+1 with unchecked items in stage N.

## Recorded values

| Key | Value |
|---|---|
| Production bundle ID | _unset_ |
| ASC app ID | _unset_ |
| ASC team ID | _unset_ |
| Subscription group | _unset_ |
| Subscription product IDs | _unset_ |
| RevenueCat project | _unset_ |
| RevenueCat entitlement | premium |
| Demo account (review) | _unset_ |
| Privacy policy URL | _unset_ |
| Support URL | _unset_ |

## Stage 0 — Prerequisites (human)

- [ ] Apple Developer Program membership active
- [ ] App Store Connect access confirmed
- [ ] ASC API key (.p8) generated and stored OUTSIDE this repo
- [ ] RevenueCat account created
- [ ] EAS CLI installed and logged in (`eas whoami` succeeds)

## Stage 1 — Local readiness (skill: submission-doctor)

- [ ] App icon (1024x1024 PNG, no alpha) added and referenced in app.config.js
- [ ] Splash image added and referenced in app.config.js
- [ ] Production bundle identifier chosen and set in app.config.js (not com.example.*)
- [ ] Privacy policy URL and support URL recorded above
- [ ] `npm run doctor` passes

## Stage 2 — ASC app record (skill: asc-setup)

- [ ] App record created in App Store Connect (name, bundle ID, SKU, primary language)
- [ ] App information complete (subtitle, category, content rights)
- [ ] App Privacy questionnaire completed
- [ ] Review information filled (contact, demo account credentials, notes)
- [ ] ASC app ID recorded above

## Stage 3 — Subscription products in ASC (skill: asc-setup)

- [ ] Subscription group created
- [ ] Each product created (reference name, product ID, duration, price)
- [ ] Localized display name + description per product
- [ ] Review screenshot uploaded per product
- [ ] Product IDs recorded above

GATE: Apple requires the FIRST subscription to be submitted together with a new app
version. Products created here stay "Missing Metadata"/"Ready to Submit" until
Stage 5 attaches them to the version. That is expected — do not retry creation.

## Stage 4 — RevenueCat (skill: revenuecat-setup)

- [ ] RevenueCat project created, iOS app added with the production bundle ID
- [ ] App Store Connect In-App Purchase Key (.p8) uploaded to RevenueCat
- [ ] Entitlement `premium` created
- [ ] Products imported/attached to the entitlement
- [ ] Default offering configured with packages
- [ ] Public iOS API key written to .env.production and .env.local (EXPO_PUBLIC_REVENUECAT_IOS_API_KEY)

## Stage 5 — Build & submit (skill: build-and-submit)

- [ ] `npm run doctor` passes
- [ ] `eas build --platform ios --profile production` succeeds
- [ ] `eas submit --platform ios --latest` succeeds
- [ ] App version created in ASC with the new build attached
- [ ] Subscriptions attached to the version (In-App Purchases section on the version page)
- [ ] Version metadata complete (description, keywords, screenshots, what's new)

## Stage 6 — App Review

- [ ] Submitted for review
- [ ] Review status checked (asc-setup skill can read the status page)
- [ ] Approved / released
```

- [ ] **Step 2: Write `scripts/submission-doctor.js`**

```js
const fs = require('fs');
const path = require('path');

process.env.NODE_ENV = 'production';

const checks = [];

function check(name, fn) {
  try {
    const result = fn();
    checks.push({ name, ok: result === true, detail: result === true ? '' : result });
  } catch (error) {
    checks.push({ name, ok: false, detail: error.message });
  }
}

let config = null;

check('app.config.js loads with production env', () => {
  config = require(path.resolve('app.config.js')).expo;
  return true;
});

check('app icon is set and exists', () => {
  if (!config) {
    return 'app.config.js did not load';
  }
  if (!config.icon) {
    return 'expo.icon is not set';
  }
  return fs.existsSync(path.resolve(config.icon)) || `icon file ${config.icon} is missing`;
});

check('splash image is set and exists', () => {
  if (!config) {
    return 'app.config.js did not load';
  }
  const splash = config.splash && config.splash.image;
  if (!splash) {
    return 'expo.splash.image is not set';
  }
  return fs.existsSync(path.resolve(splash)) || `splash file ${splash} is missing`;
});

check('production bundle identifier is customised', () => {
  if (!config) {
    return 'app.config.js did not load';
  }
  const id = (config.ios && config.ios.bundleIdentifier) || '';
  return !id.includes('com.example.') || `bundle id ${id} still uses com.example.*`;
});

check('encryption declaration present', () => {
  if (!config) {
    return 'app.config.js did not load';
  }
  const plist = (config.ios && config.ios.infoPlist) || {};
  return plist.ITSAppUsesNonExemptEncryption !== undefined || 'ITSAppUsesNonExemptEncryption missing from infoPlist';
});

check('.env.production has the RevenueCat iOS API key', () => {
  if (!fs.existsSync('.env.production')) {
    return '.env.production is missing';
  }
  const content = fs.readFileSync('.env.production', 'utf8');
  return /EXPO_PUBLIC_REVENUECAT_IOS_API_KEY=.+/.test(content) || 'EXPO_PUBLIC_REVENUECAT_IOS_API_KEY is not set';
});

check('react-native-purchases is installed', () => {
  const pkg = JSON.parse(fs.readFileSync('package.json', 'utf8'));
  return Boolean(pkg.dependencies['react-native-purchases']) || 'react-native-purchases missing from dependencies';
});

check('eas.json has a production build profile', () => {
  const eas = JSON.parse(fs.readFileSync('eas.json', 'utf8'));
  return Boolean(eas.build && eas.build.production) || 'production build profile missing';
});

check('SUBMISSION.md exists', () => fs.existsSync('SUBMISSION.md') || 'SUBMISSION.md is missing');

for (const result of checks) {
  const mark = result.ok ? 'PASS' : 'FAIL';
  const detail = result.ok ? '' : ` — ${result.detail}`;
  console.log(`${mark}  ${result.name}${detail}`);
}

if (checks.some((result) => !result.ok)) {
  process.exit(1);
}
console.log('submission-doctor: all checks passed');
```

- [ ] **Step 3: Verify the doctor fails on the fresh template (expected — no icon, example bundle id)**

Run (in `expo/templates/app`): `npm run doctor`
Expected: exit 1; FAIL lines for icon, splash, bundle identifier, `.env.production`; PASS for config load, encryption declaration, react-native-purchases, eas.json, SUBMISSION.md.

- [ ] **Step 4: Write `.claude/skills/submission-doctor/SKILL.md`**

```markdown
---
name: submission-doctor
description: Use when checking App Store submission readiness, when asked "are we ready to submit", or before starting any submission stage
---

# Submission Doctor

Audits local submission readiness and reports the current stage.

## Steps

1. Run `npm run doctor`. For every FAIL line, explain the fix concretely
   (which file, which field, what value).
2. Read `SUBMISSION.md`. Find the FIRST stage that still has unchecked items.
3. Report to the user:
   - Doctor results (pass/fail summary)
   - Current stage and its remaining unchecked items
   - The skill that owns the next stage:

| Stage | Skill |
|---|---|
| 0 — Prerequisites | human task; verify with `eas whoami` and by asking the user |
| 1 — Local readiness | this skill (doctor) + direct edits |
| 2 — ASC app record | asc-setup |
| 3 — Subscription products | asc-setup |
| 4 — RevenueCat | revenuecat-setup |
| 5 — Build & submit | build-and-submit |
| 6 — App Review | build-and-submit |

## Rules

- Never check a SUBMISSION.md box without verifying the underlying fact
  (file exists, command succeeds, page state observed).
- Never start a stage while an earlier stage has unchecked items.
- If the doctor passes but SUBMISSION.md disagrees (or vice versa), trust the
  verification, fix SUBMISSION.md, and tell the user what was stale.
```

- [ ] **Step 5: Commit**

```powershell
git add expo; git commit -m "Add SUBMISSION.md state file and submission-doctor"
```

---

### Task 11: asc-setup skill

**Files:**
- Create: `expo/templates/app/.claude/skills/asc-setup/SKILL.md`

- [ ] **Step 1: Write `SKILL.md`**

```markdown
---
name: asc-setup
description: Use for App Store Connect work - creating the app record, filling metadata, creating subscription products, or checking review status (Stages 2, 3, and 6 of SUBMISSION.md)
---

# App Store Connect Setup (Playwright-driven)

Drives https://appstoreconnect.apple.com via Playwright MCP browser tools.

## Preconditions

- Read SUBMISSION.md first. Only run the stage it says is next.
- Playwright MCP available. Navigate to App Store Connect; if a login or 2FA
  screen appears, STOP and ask the user to complete login in the browser
  window, then continue.

## Idempotency protocol (mandatory)

Before creating ANYTHING: snapshot the current page and check whether the
object already exists (app record in the Apps list, subscription group on the
Subscriptions page, product in the group). If it exists, verify its fields
match SUBMISSION.md, record its IDs, check the box, and move on. Re-running
any stage must always be safe.

On ANY unexpected page state (selector missing, unfamiliar layout, error
banner): stop, snapshot, report what you saw. Never guess-click.

## Stage 2 — App record

1. Apps → "+" → New App. Platform iOS; Name, Primary Language, Bundle ID
   (must match `ios.bundleIdentifier` in app.config.js — cross-check), SKU
   (use the bundle id), full access.
2. App Information: subtitle, category. Content rights declaration.
3. App Privacy: ask the user which data types the app collects before
   answering the questionnaire — never guess privacy answers.
4. App Review information: contact details, demo account (record the
   credentials location in SUBMISSION.md — credentials themselves go in a
   password manager, NOT in the repo), review notes.
5. Record the ASC app ID (from the URL: /apps/<ID>/) in SUBMISSION.md.
6. Check off completed Stage 2 items.

## Stage 3 — Subscription products

1. App page → Monetization → Subscriptions. Create the subscription group if
   missing (one group unless the user wants tiers to coexist).
2. Per product: Reference Name, Product ID (reverse-DNS style, e.g.
   `monthly_premium` — must match what RevenueCat will import in Stage 4),
   duration, price. Localization: display name + description. Review
   screenshot (1242x2208 or device-equivalent) — ask the user to provide one
   or generate from the running app.
3. Expect final status "Missing Metadata" or "Ready to Submit" — NOT
   "Approved". Apple requires the first subscription to ship with a new app
   version (Stage 5 attaches it). Do not retry; record product IDs and Apple
   IDs in SUBMISSION.md and check off Stage 3.

## Stage 6 — Review status

Navigate to the app's Distribution page and report the current version
status (Waiting for Review / In Review / Rejected / Ready for Sale). If
rejected, open Resolution Center, extract the rejection reasons verbatim,
and report them.
```

- [ ] **Step 2: Commit**

```powershell
git add expo; git commit -m "Add asc-setup submission skill"
```

---

### Task 12: revenuecat-setup skill

**Files:**
- Create: `expo/templates/app/.claude/skills/revenuecat-setup/SKILL.md`

- [ ] **Step 1: Write `SKILL.md`**

```markdown
---
name: revenuecat-setup
description: Use for RevenueCat configuration - project, entitlements, offerings, API keys (Stage 4 of SUBMISSION.md)
---

# RevenueCat Setup (Playwright-driven)

Drives https://app.revenuecat.com via Playwright MCP browser tools.

## Preconditions

- SUBMISSION.md Stages 2 and 3 complete (product IDs recorded — RevenueCat
  imports them from ASC, so they must exist first).
- If a login screen appears, STOP and ask the user to log in, then continue.

## Idempotency protocol (mandatory)

Snapshot before creating anything. If the project/app/entitlement/offering
already exists, verify its configuration matches SUBMISSION.md, record what
is missing, and only create the missing pieces. On unexpected page state:
stop, snapshot, report.

## Steps

1. Create a project named after the app (or reuse the existing one).
2. Add an iOS app to the project with the PRODUCTION bundle ID from
   SUBMISSION.md.
3. App Store Connect API: RevenueCat needs the In-App Purchase Key. Ask the
   user to generate/locate the .p8 In-App Purchase Key in ASC (Users and
   Access → Integrations → In-App Purchase) and upload it in the RevenueCat
   app settings. Pause for the user — never handle the .p8 contents yourself.
4. Products: import the product IDs recorded in SUBMISSION.md Stage 3.
5. Entitlement: create `premium` (must match PREMIUM_ENTITLEMENT in
   src/lib/purchases/useSubscription.ts — cross-check before creating).
   Attach all products to it.
6. Offering: configure the `default` offering with one package per product
   (monthly → $rc_monthly, annual → $rc_annual).
7. Copy the PUBLIC iOS API key (Project settings → API keys → Public
   app-specific). Write it to `.env.production` and `.env.local` as
   `EXPO_PUBLIC_REVENUECAT_IOS_API_KEY=<key>`. These files are gitignored —
   confirm with `git check-ignore .env.production` before writing.
8. Update SUBMISSION.md: record the project name, check off Stage 4.
9. Run `npm run doctor` — the `.env.production` check should now pass.
```

- [ ] **Step 2: Commit**

```powershell
git add expo; git commit -m "Add revenuecat-setup submission skill"
```

---

### Task 13: build-and-submit skill

**Files:**
- Create: `expo/templates/app/.claude/skills/build-and-submit/SKILL.md`

- [ ] **Step 1: Write `SKILL.md`**

```markdown
---
name: build-and-submit
description: Use for EAS builds and App Store submission - production build, eas submit, attaching subscriptions to the version, submitting for review (Stage 5 of SUBMISSION.md)
---

# Build & Submit (EAS CLI + Playwright for the version page)

## Preconditions

- SUBMISSION.md Stages 0-4 all checked.
- `npm run doctor` passes (run it now; if it fails, route to
  submission-doctor and stop).
- `eas whoami` succeeds. If not, ask the user to run `! eas login`.

## Build

1. If `app.config.js` `extra.eas.projectId` is missing, run `eas init` and
   commit the change (follow the dev lifecycle in CLAUDE.md - issue, branch,
   PR).
2. Run `eas build --platform ios --profile production --non-interactive`.
   First-ever build needs Apple credentials setup, which is interactive:
   if the command fails asking for credentials, ask the user to run
   `! eas build --platform ios --profile production` themselves once, then
   resume here.
3. Stream/poll the build with `eas build:list --platform ios --limit 1`.
   On failure, fetch the log URL it prints, read the failing phase, report
   the root cause, and fix before retrying.

## Submit

4. Run `eas submit --platform ios --latest`. This uploads the build to ASC.
5. Wait for the build to finish processing in ASC (TestFlight tab shows it;
   processing can take 5-30 min). Poll via asc-setup style Playwright reads.

## Attach IAP + metadata (Playwright)

6. On the ASC version page: create the version if ASC has not auto-created
   one; select the processed build.
7. In-App Purchases and Subscriptions section: attach the subscriptions
   recorded in SUBMISSION.md Stage 3. This satisfies Apple's
   first-subscription-ships-with-a-version rule.
8. Version metadata: description, keywords, support URL, screenshots
   (6.7" and 6.1" minimum), what's new. Ask the user for anything missing.
9. Export compliance: already declared via ITSAppUsesNonExemptEncryption
   in app.config.js, confirm no prompt blocks submission.

## Submit for review

10. Press "Add for Review" / "Submit to App Review". Snapshot the
    confirmation state.
11. Update SUBMISSION.md: check Stage 5 items and the Stage 6 "Submitted"
    box; record the version number.
12. Tell the user review typically takes 24-72h and that the asc-setup
    skill checks status on request.
```

- [ ] **Step 2: Commit**

```powershell
git add expo; git commit -m "Add build-and-submit submission skill"
```

---

### Task 14: Template CLAUDE.md (the orchestrator)

**Files:**
- Create: `expo/templates/app/CLAUDE.md`, `expo/templates/app/README.md`

- [ ] **Step 1: Write `CLAUDE.md`**

```markdown
# CLAUDE.md

Project context for Claude Code sessions. Read this before making changes.

## Development lifecycle

Every change follows this loop. None of these steps are optional — hooks
enforce each transition.

1. **Open an issue.** `gh issue create --title "..."`. No issue, no branch.
2. **Create a feat branch.** `git checkout -b feat/<N>-<kebab-slug>` where
   `<N>` is the issue number. `.githooks/reference-transaction` rejects the
   branch if the name doesn't match or issue #N doesn't exist.
3. **Edit + test.** `npm run verify` runs the full guardrail set locally.
4. **Commit.** `.githooks/pre-commit` runs: branch guard, merged-branch
   check, `tsc --noEmit`, `eslint . --max-warnings 0`, dependency-cruiser,
   check-test-files, `jest --coverage`. Any failure blocks the commit.
5. **Open PR.** `gh pr create --base main --head feat/<N>-<slug>`.
6. **Squash merge.** `gh pr merge <N> --squash --delete-branch`.

Direct edits and commits to `main` are blocked. Edits to an already-merged
branch are blocked (Claude Code PreToolUse hooks + pre-commit).

## Code style

- **No comments.** `local/no-comments` fires at error severity. Extract
  intent into function, variable, or type names.
- **No inline rule escapes.** ESLint runs with `noInlineConfig` —
  `eslint-disable` comments do nothing (and are themselves lint errors).
- **No `any`.** `@typescript-eslint/no-explicit-any` at error severity.
- **Caps:** 60 lines per function, 4 parameters, 300 lines per file, one
  exported component per file.
- **Strict TypeScript:** `strict`, `noUncheckedIndexedAccess`,
  `noImplicitOverride`.

## Architecture

- `src/app/` — screens and navigation. May import anything.
- `src/components/` — shared presentational components. May import `lib`
  only.
- `src/features/<name>/` — vertical feature slices. Never import a sibling
  feature.
- `src/lib/` — platform/service wrappers (purchases, storage, api). Never
  imports app/features/components.
- dependency-cruiser enforces all of the above at error severity
  (`.dependency-cruiser.cjs`).
- Every module in `src/lib` and `src/features` must have a
  `__tests__/<name>.test.ts(x)` file (`scripts/check-test-files.js`) and
  coverage ≥ 80% lines/branches.
- Subscriptions: `src/lib/purchases/` wraps RevenueCat. The entitlement id
  is `PREMIUM_ENTITLEMENT` in `useSubscription.ts` and must match the
  RevenueCat dashboard.

## App Store submission — orchestration

`SUBMISSION.md` is the single source of truth for submission state. On ANY
submission-related request ("set up the app store", "create the products",
"submit the app", "are we ready?"):

1. **Read `SUBMISSION.md` first.** Find the first stage with unchecked
   items — that is the current stage. Never skip ahead.
2. **Invoke the skill that owns the stage:**

| Stage | Owner |
|---|---|
| 0 Prerequisites | human (verify, don't automate) |
| 1 Local readiness | submission-doctor |
| 2 ASC app record | asc-setup |
| 3 Subscription products | asc-setup |
| 4 RevenueCat | revenuecat-setup |
| 5 Build & submit | build-and-submit |
| 6 App Review | build-and-submit / asc-setup (status) |

3. **Update `SUBMISSION.md` after every completed step** — check boxes,
   record IDs/values. Commit SUBMISSION.md changes through the normal dev
   lifecycle.
4. **Secrets never enter the repo.** `.p8` keys, demo account passwords,
   and API keys live outside git (`.env.*` files are gitignored; `*.p8` is
   gitignored as a backstop).
5. **Stop at human gates.** Login/2FA screens, App Privacy answers, pricing
   decisions, and review screenshots need the user — pause and ask.

## Key files

- `SUBMISSION.md` — submission state machine
- `.claude/skills/` — submission-doctor, asc-setup, revenuecat-setup,
  build-and-submit
- `scripts/submission-doctor.js` — local readiness checks (`npm run doctor`)
- `.githooks/` + `.claude/hooks/` — lifecycle enforcement
- `eslint.config.js` + `eslint-rules/` — style enforcement
- `.dependency-cruiser.cjs` — layering enforcement
```

- [ ] **Step 2: Write the template `README.md`**

```markdown
# AppTemplate

Expo app scaffolded from the agent-harness expo template: error-severity
guardrails for agent-driven development plus a staged, resumable iOS App
Store submission workflow.

## Quick start

```powershell
.\setup.ps1
npm run verify
npx expo start
```

Note: `react-native-purchases` is a native module — use a development build
(`eas build --profile development` or `npx expo run:ios`), not Expo Go.

## Guardrails (all error severity)

- `npm run typecheck` — strict TypeScript
- `npm run lint` — ESLint incl. local rules: no comments, one exported
  component per file; inline eslint-disable is inert
- `npm run depcruise` — layer rules (lib ← components ← app; features
  isolated)
- `npm run check-test-files` — every lib/features module has a test file
- `npm run test -- --coverage` — 80% lines/branches on lib + features
- `npm run verify` — all of the above
- `.githooks/pre-commit` — runs the lot before every commit

## App Store submission

Read `SUBMISSION.md` (the state machine), then ask Claude:
"run the submission doctor". CLAUDE.md routes each stage to the right
skill (asc-setup, revenuecat-setup, build-and-submit).
```

- [ ] **Step 3: Commit**

```powershell
git add expo; git commit -m "Add orchestrating CLAUDE.md and README to expo template"
```

---

### Task 15: Template validation script + CI

**Files:**
- Create: `expo/template-tests/scaffold-and-validate.ps1`
- Modify: `.github/workflows/template-ci.yml`

- [ ] **Step 1: Write `expo/template-tests/scaffold-and-validate.ps1`**

```powershell
$ErrorActionPreference = 'Stop'

$expoRoot = Split-Path -Parent $PSScriptRoot
$scaffoldDir = Join-Path $env:TEMP 'expo-template-smoke'

Write-Host "Cleaning previous smoke-test directory..."
if (Test-Path $scaffoldDir) {
    Remove-Item -Recurse -Force $scaffoldDir
}

& (Join-Path $expoRoot 'new-app.ps1') -Name SmokeTest -Destination $scaffoldDir

Push-Location $scaffoldDir
try {
    Write-Host "Installing dependencies..."
    npm install
    if ($LASTEXITCODE -ne 0) { throw 'npm install failed' }

    Write-Host "Typecheck..."
    npm run typecheck
    if ($LASTEXITCODE -ne 0) { throw 'typecheck failed' }

    Write-Host "Lint..."
    npm run lint
    if ($LASTEXITCODE -ne 0) { throw 'lint failed' }

    Write-Host "Dependency rules..."
    npm run depcruise
    if ($LASTEXITCODE -ne 0) { throw 'depcruise failed' }

    Write-Host "Test-file check..."
    npm run check-test-files
    if ($LASTEXITCODE -ne 0) { throw 'check-test-files failed' }

    Write-Host "Tests + coverage..."
    npx jest --coverage --silent
    if ($LASTEXITCODE -ne 0) { throw 'tests failed' }

    Write-Host "Seeded violation: no-comments must fire..."
    Set-Content -Path 'src\lib\seeded.ts' -Value "// seeded violation`nexport const seeded = 1;"
    npx eslint src/lib/seeded.ts
    if ($LASTEXITCODE -eq 0) { throw 'no-comments rule did not fire on a seeded violation' }
    Remove-Item 'src\lib\seeded.ts'

    Write-Host "Doctor must fail on a fresh scaffold (no icon, example bundle id)..."
    node scripts/submission-doctor.js
    if ($LASTEXITCODE -eq 0) { throw 'submission-doctor unexpectedly passed on a fresh scaffold' }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Expo template validation passed. Cleaning up..."
Remove-Item -Recurse -Force $scaffoldDir
Write-Host "Done."
```

- [ ] **Step 2: Run it locally**

Run: `.\expo\template-tests\scaffold-and-validate.ps1`
Expected: ends with `Expo template validation passed.` then `Done.`

- [ ] **Step 3: Add the expo job to CI**

Append to `.github/workflows/template-ci.yml` (after the existing `scaffold-and-test` job, same indentation level):

```yaml
  expo-scaffold-and-validate:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-node@v4
        with:
          node-version: '22'

      - name: Scaffold and validate expo template
        shell: pwsh
        run: ./expo/template-tests/scaffold-and-validate.ps1
```

- [ ] **Step 4: Commit and verify CI**

```powershell
git add expo .github; git commit -m "Add expo template validation script and CI job"
git push
gh run watch
```
Expected: both `scaffold-and-test` (cli, etl-api) and `expo-scaffold-and-validate` jobs green.

---

### Task 16: Root README rewrite

**Files:**
- Create: `README.md` (root)

- [ ] **Step 1: Write the new root `README.md`**

```markdown
# agent-harness

Project templates designed for letting an AI agent — Claude Code, Codex, or
anything similar — write the code. One philosophy, multiple stacks:

> Soft rules in natural language don't reliably survive a long agent
> session. The only reliable way to enforce a rule on an agent is to make
> it **stop the build**: a failing diagnostic, a failing test, a rejected
> commit. The agent reads the error and self-corrects.

Every template ships guardrails wired into the toolchain at **error
severity**, plus a CLAUDE.md documenting the lifecycle (issue → branch →
commit → PR) and four git/Claude hooks enforcing it.

## Harnesses

| Stack | Where | Templates | Extras |
|---|---|---|---|
| .NET 10 | [`dotnet/`](dotnet/README.md) | `cli`, `etl-api` | 15 Roslyn analyzers (CI0001–CI0015), 5 architecture-test fixtures |
| Expo / React Native | [`expo/`](expo/) | `app` | Strict TS + custom ESLint rules + dependency-cruiser + coverage gates, RevenueCat baked in, staged iOS App Store **submission workflow** driven by Claude skills (SUBMISSION.md state machine) |

## .NET

```powershell
git clone https://github.com/ryan75195/dotnet-agent-harness
dotnet new install .\dotnet-agent-harness\dotnet
dotnet new cli -n MyTool
cd MyTool
.\setup.ps1
```

Full docs: [`dotnet/README.md`](dotnet/README.md)

## Expo

```powershell
git clone https://github.com/ryan75195/dotnet-agent-harness
.\dotnet-agent-harness\expo\new-app.ps1 -Name MyApp
cd MyApp
.\setup.ps1
```

What you get:

- **Guardrails at error severity:** no comments (`local/no-comments` with
  inline-disable comments made inert), one exported component per file,
  60-line functions, no `any`, strict tsconfig, dependency-cruiser layer
  rules (`lib ← components ← app`, features isolated), 80% coverage on
  `lib`/`features`, a test file required for every module, and a pre-commit
  hook that runs the lot.
- **Submission harness:** `SUBMISSION.md` is a staged state machine from
  prerequisites to App Review. The template's CLAUDE.md routes each stage
  to a Claude skill — `submission-doctor` (local readiness audit),
  `asc-setup` (Playwright-drives App Store Connect: app record,
  subscription products, metadata), `revenuecat-setup` (Playwright-drives
  RevenueCat: entitlements, offerings, API keys), `build-and-submit`
  (EAS build/submit + attaching IAP to the version). Skills are idempotent
  and resumable; secrets never enter the repo.
- **RevenueCat baked in:** `src/lib/purchases/` wraps configuration and a
  `useSubscription` hook; a paywall screen renders the current offering.

## CI

`.github/workflows/template-ci.yml` scaffolds, builds, and validates every
template on each push: `dotnet build` + `dotnet test` for the .NET
templates; typecheck + lint + dependency rules + tests + seeded-violation
checks for the Expo template.

## Development

```powershell
.\dotnet\template-tests\scaffold-and-build.ps1 cli
.\dotnet\template-tests\scaffold-and-build.ps1 etl-api
.\expo\template-tests\scaffold-and-validate.ps1
```
```

- [ ] **Step 2: Commit**

```powershell
git add README.md; git commit -m "Rewrite root README as multi-language harness index"
git push
```

- [ ] **Step 3 (optional, manual):** Rename the GitHub repo to `agent-harness` (Settings → General). GitHub redirects the old URL. Update the clone URLs in both READMEs afterwards if renamed.

---

## Self-review notes (already applied)

- Spec coverage: restructure (T1), template+RevenueCat (T2–T7), guardrails (T4, T5, T8), scaffolding (T9), SUBMISSION.md + doctor (T10), Playwright skills (T11–T13), CLAUDE.md orchestrator (T14), validation+CI (T15), README (T16). Spec's "80% lines/branches" → `jest.config.js` coverageThreshold. Spec's "seeded violation proving custom rules fire" → T15 Step 1.
- Type consistency: `PREMIUM_ENTITLEMENT` defined once in `useSubscription.ts`, referenced by revenuecat-setup skill and CLAUDE.md. `EXPO_PUBLIC_REVENUECAT_IOS_API_KEY` consistent across `.env.example`, `app.config.js`, `initPurchases.ts`, doctor, and the revenuecat skill.
- Known risk, accepted: pinned dep versions may drift; Task 2 Step 6 (`npx expo install --fix`) reconciles expo-managed versions at implementation time.
```
