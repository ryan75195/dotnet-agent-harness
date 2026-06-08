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
