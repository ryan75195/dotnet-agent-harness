# AppTemplate

![CI](https://github.com/YOUR_GITHUB_USER/YOUR_REPO/actions/workflows/ci.yml/badge.svg)

Expo app scaffolded from the agent-harness expo template: error-severity
guardrails for agent-driven development plus a staged, resumable iOS App
Store submission workflow.

After `gh repo create`, replace `YOUR_GITHUB_USER/YOUR_REPO` in the badge above
with your repository path.

## Quick start

```powershell
.\setup.ps1
npm run verify
npx expo start
```

Navigation is file-based (expo-router): routes live in `src/app/`, screens in
`src/features/`.

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

## Continuous integration

`.github/workflows/ci.yml` runs the full guardrail set (`npm run verify`) on
every pull request and on pushes to `main` — the same checks the local
`.githooks/pre-commit` runs, enforced on GitHub's runners so they cannot be
skipped with `--no-verify`.

### Add an EAS build to CI (optional)

To also build on CI, add an `EXPO_TOKEN` repository secret (from
`expo.dev` → Account → Access tokens) and append this job to `ci.yml`:

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

## Account deletion endpoint

Apple requires in-app account deletion (Guideline 5.1.1(v)) for apps with
accounts. The app ships the full client flow (Settings → Delete account →
confirm). The actual deletion runs on YOUR backend, because deleting an Auth0
user needs the Management API client secret, which must never be in the app.

Set `EXPO_PUBLIC_ACCOUNT_DELETE_URL` to an endpoint that:

1. Accepts `DELETE` with header `Authorization: Bearer <user access token>`.
2. Verifies the token against the Auth0 JWKS and extracts the `sub` claim.
3. Calls the Auth0 Management API to delete that user.
4. Returns `204` on success.

Reference (Node, illustrative — not shipped):

```js
import { createRemoteJWKSet, jwtVerify } from 'jose';

const jwks = createRemoteJWKSet(new URL(`https://${AUTH0_DOMAIN}/.well-known/jwks.json`));

export async function handleDelete(request) {
  const token = request.headers.authorization?.replace('Bearer ', '');
  const { payload } = await jwtVerify(token, jwks, { issuer: `https://${AUTH0_DOMAIN}/` });
  await fetch(`https://${AUTH0_DOMAIN}/api/v2/users/${encodeURIComponent(payload.sub)}`, {
    method: 'DELETE',
    headers: { Authorization: `Bearer ${MANAGEMENT_API_TOKEN}` }
  });
  return new Response(null, { status: 204 });
}
```
