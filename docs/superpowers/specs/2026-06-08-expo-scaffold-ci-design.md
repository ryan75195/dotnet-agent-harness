# Expo Scaffold CI Workflow — Design

**Date:** 2026-06-08
**Status:** Approved

## Goal

Bake a GitHub Actions CI workflow into the Expo template so every scaffolded app
gets server-side enforcement of the same guardrail set the local pre-commit hook
runs. Today a scaffolded app has only `.githooks/pre-commit` (local, skippable
with `--no-verify`); there is no hosted CI. After this change, opening a PR on a
scaffolded app runs typecheck + lint + dependency-cruiser + check-test-files +
jest coverage on GitHub's runners.

## Scope decisions

| Decision | Choice |
|---|---|
| CI scope | Guardrails only (`npm run verify`). No EAS build/submit, no secrets. |
| EAS in CI | Documented opt-in in the template README; not wired by default. |
| Triggers | `pull_request` + `push` to `main`, matching the harness's own workflow. |
| Status badge | Included in the template README as an obvious placeholder for the user's repo. |

## 1. The mechanism (why it is safe in the harness repo)

The workflow file lives at `expo/templates/app/.github/workflows/ci.yml` — nested
inside the template directory. GitHub only executes workflows found in
`.github/workflows/` at a repository **root**. A nested copy is inert: it never
triggers in the harness repo. It becomes active only when `new-app.ps1` copies
the template to a new repo root and the user pushes to GitHub. So scaffolded apps
gain CI while the harness repo's Actions are unaffected.

`new-app.ps1` already copies the whole template tree via robocopy (excluding
`node_modules`, `.expo`, `coverage`, `.git`). The `.github` directory is copied
like any other. The workflow contains no app-name placeholders (all generic
`npm`), so the rename loop needs no change.

## 2. The workflow (`ci.yml`)

```yaml
name: ci
on:
  pull_request:
  push:
    branches: [main]

jobs:
  verify:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'npm'
      - run: npm ci
      - run: npm run verify
```

- `runs-on: ubuntu-latest` — the guardrails are platform-agnostic (Node/jest/
  eslint), so Linux runners are fine and cheap. (The harness's own template CI
  uses Windows because it scaffolds; a scaffolded app has no such need.)
- Node 22 matches the template `.nvmrc`.
- `cache: 'npm'` keyed on `package-lock.json` for fast installs.
- `npm run verify` is the single composed gate: typecheck → lint
  (`--max-warnings 0`) → depcruise → check-test-files → jest `--coverage`.

### `npm ci` correctness

`npm ci` requires `package-lock.json` to match `package.json`. The scaffold flow
guarantees this: `setup.ps1` runs `npm install` (reconciling the lockfile after
`new-app.ps1` renamed the package) before the first commit, so by the time the
app is on GitHub the lockfile is consistent. `npm ci` is therefore deterministic
and correct. If template validation surfaces lockfile drift, the documented
fallback is `npm install`, but `npm ci` is the intended path.

## 3. Template README changes

- **CI status badge** near the top, as an explicit placeholder:
  `![CI](https://github.com/YOUR_GITHUB_USER/YOUR_REPO/actions/workflows/ci.yml/badge.svg)`
  with a one-line note to replace `YOUR_GITHUB_USER/YOUR_REPO` after
  `gh repo create`.
- A short **"Add an EAS build to CI"** section showing the opt-in job and the
  `EXPO_TOKEN` secret, so the user can extend CI to builds when ready without it
  being wired (and failing) by default:

  ```yaml
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '22', cache: 'npm' }
      - run: npm ci
      - uses: expo/expo-github-action@v8
        with:
          eas-version: latest
          token: ${{ secrets.EXPO_TOKEN }}
      - run: eas build --platform ios --profile preview --non-interactive
  ```

## 4. CLAUDE.md change

The "Development lifecycle" section notes that CI re-runs the same `verify` gate
on every PR (so the agent knows the server enforces what the local hook does),
and the "Key files" list gains `.github/workflows/ci.yml`.

## 5. Validation

`expo/template-tests/scaffold-and-validate.ps1` gains one structural assertion
after scaffolding: assert `.github/workflows/ci.yml` exists in the scaffold and
parses as valid YAML (GitHub Actions cannot be executed locally, so this is a
presence + parse check, not an execution test). The harness's own
`template-ci.yml` runs the validation script unchanged, so this is covered in CI.

## Out of scope

- EAS build/submit wired into CI by default (documented opt-in only).
- Secrets management, matrix builds, Android jobs, caching beyond npm.
- A hosted CI for the harness repo's own development (already exists as
  `template-ci.yml`).
- Auto-renaming the badge URL to the user's repo (the user sets the repo slug at
  `gh repo create` time; the placeholder is intentional).
