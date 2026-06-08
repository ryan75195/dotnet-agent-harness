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
