# .NET Azure Durable Functions template — design

Date: 2026-07-15
Status: Approved (design), pending implementation plan

## Problem

`agent-project-templates` ships `dotnet new` templates for a CLI, an ETL+API
service, and an MCP server, each wired into the `new-project` CLI, the plugin
skills, provenance stamping, and CI. All three assume the work fits inside one
process lifetime.

Nothing covers **workflows that outlive a process**: kick off an external
system, park for minutes-to-hours, resume when it calls back. The driving use
case is orchestrating cloud agents tied to GitHub issues via webhooks — an
agent run takes far too long to hold a thread or an HTTP request open, and a
crash mid-run must not lose the work. That is precisely the workload Azure
Durable Functions exists for, and it cannot be faked with the `cli` template's
in-process loop.

This adds a `dotnet new durable` template following the same house style
(guardrails, analyzers, architecture tests, DI/Core layering, agent configs) and
wires it into the ecosystem as a first-class type like the others.

## Goals

- A `dotnet new durable` template producing a .NET 10 isolated-worker Durable
  Functions solution that builds, tests, and passes the full guardrail suite out
  of the box.
- Demonstrate the durable patterns the driving use case needs: webhook-started
  orchestration with a deterministic instance ID, fan-out/fan-in, sub-
  orchestration, **external event raced against a durable timer**, and a durable
  entity.
- **Enforce orchestrator determinism at error severity** — Microsoft's bundled
  `DURABLE*` analyzers pinned to `error`, plus three new custom analyzers closing
  the gaps those rules leave, plus an architecture rule that makes the most
  dangerous mistake structurally impossible.
- A real integration test proving an orchestration replays and completes
  end-to-end against Azurite.
- Full ecosystem integration: provenance stamp, `new-project` CLI handler, plugin
  skill, CI, plugin validation.

## Non-goals

- **No Durable Task Scheduler (DTS) backend in the scaffold.** Azure Storage +
  Azurite is the default; DTS is a documented README switch. Rationale below.
- **No GitHub-specific code.** The sample is agent-*shaped* but the external
  system is stubbed behind a Core interface. No webhook HMAC verification, no
  Octokit, no issue/comment activities. The forker deletes the sample the same
  way they delete `mcp`'s `EchoTool`.
- **No deployment infrastructure** (no Bicep/Terraform, no `azd`). Scaffolding a
  project, not deploying one.
- No authentication on the HTTP triggers (Functions-level auth is deployment-
  specific; documented extension point).
- No changes to the existing cli/etl-api/mcp templates beyond shared-skeleton
  parity.

## Design decisions (from brainstorming)

- **Scope:** generic Durable Functions template with an agent-shaped sample —
  consistent with how `cli`/`etl-api`/`mcp` ship throwaway samples.
- **Backend:** Azure Storage + Azurite.
- **Guardrails:** Microsoft's analyzers pinned to error, **plus** custom analyzers
  for the gaps, **plus** durable-specific architecture tests.
- **Sample surface:** the full set — webhook → fan-out → dispatch → await callback
  → timeout, plus a durable entity.
- **Integration tests:** real, against Azurite, with a new CI step.
- **Analyzer staging:** all three custom analyzers land together.

### Why Azure Storage and not DTS

Microsoft now names DTS the *recommended* backend for new apps, but Azure Storage
remains the *default* — those are different words and the docs use both
deliberately. Three reasons the default wins here:

1. DTS local dev and CI require the Docker emulator image
   (`mcr.microsoft.com/dts/dts-emulator`). The .NET CI matrix runs on
   `windows-latest`, where **GitHub Actions does not support service containers at
   all**, and Linux containers are impractical.
2. A hard Docker dependency in `setup.ps1` cuts against the repo's governing
   principle that templates are standalone and runnable in a plain terminal.
3. Azurite needs no extra NuGet package and `func start` auto-starts it when
   `AzureWebJobsStorage=UseDevelopmentStorage=true`.

The backend is a `host.json` + package concern, so a project that outgrows Azure
Storage switches by adding
`Microsoft.Azure.Functions.Worker.Extensions.DurableTask.AzureManaged` and setting
`storageProvider.type = "azureManaged"`. The README documents this. Netherite is
explicitly not considered — it retires 2028-03-31.

## Architecture

New template directory `dotnet/templates/durable`, a .NET 10 solution mirroring
`dotnet/templates/mcp`, swapping the MCP host for a Functions isolated host.

### Project layout

```
dotnet/templates/durable/
  .template.config/template.json     (shortName durable, sourceName SampleDurable)
  SampleDurable.slnx
  Directory.Build.props
  setup.ps1, README.md, CLAUDE.md, harness-manifest.json
  .editorconfig, .gitignore
  host.json, local.settings.json
  .claude/ .githooks/                 (agent configs + guardrail hooks)
  src/
    SampleDurable.Functions/          isolated-worker Functions host
      Program.cs
      Triggers/         (HTTP triggers holding [DurableClient])
      Orchestrations/   ([OrchestrationTrigger])
      Activities/       ([ActivityTrigger])
      Entities/         ([EntityTrigger])
      AssemblyMarker.cs
    SampleDurable.Core/
      Models/           DTOs — orchestrations may depend on these
      Interfaces/       IAgentDispatcher, IResultPublisher — orchestrations may NOT
      Services/         stub implementations — orchestrations may NOT
      ServiceCollectionExtensions.cs
      AssemblyMarker.cs
    SampleDurable.Analyzers/          CI0001-CI0015 (copied) + CI0016-CI0018 (new)
  tests/
    SampleDurable.Tests.Unit/         orchestrator logic w/ substituted context
    SampleDurable.Tests.Integration/  real host + Azurite, end-to-end replay
    SampleDurable.Tests.Architecture/ layer/DI/naming + new durable rules
    SampleDurable.Tests.Analyzers/    analyzer tests (copied + 3 new fixtures)
```

`Core` is split `Models/` vs `Interfaces/`+`Services/` deliberately — that split is
what the central architecture rule keys off. It is not cosmetic.

### Copied vs. new

**Copied from mcp** (identical guardrail skeleton, `SampleMcp`→`SampleDurable`
renames): the `Analyzers` project (CI0001–CI0015) and its tests, the
`Architecture` test harness (`TestHelpers.cs`, `LayerDependencyTests`,
`NamingConventionTests`, `ServiceShapeTests`, `CodeStructureTests`,
`DiRegistrationTests`), `.claude` agent configs, `.githooks`, `.editorconfig`,
`Directory.Build.props`, `setup.ps1`, `harness-manifest.json`, `.gitignore`.

**Follow `mcp`, not `etl-api`, where they diverge.** `etl-api` is the outlier: it
alone ships a `.codex/` directory and a third `.claude` hook
(`block-mutating-shell-on-main-branch.sh`). `cli` and `mcp` ship neither. The
`durable` template matches `cli`/`mcp` — no `.codex/`, two `.claude` hooks.
Reconciling that divergence across templates is out of scope here; this template
just does not widen it.

**New / unique:** the `Functions` project, the `Core` services, CI0016–CI0018 and
their test fixtures, `DurableFunctionTests`, the Azurite integration test,
`host.json`/`local.settings.json`, `template.json`, the `.slnx`, and
README/CLAUDE.md content.

### Packages

Verified current as of 2026-07-15:

| Package | Version |
|---|---|
| `Microsoft.Azure.Functions.Worker` | 2.52.0 (2.50.0 is the .NET 10 floor) |
| `Microsoft.Azure.Functions.Worker.Sdk` | 2.0.7 |
| `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` | 1.18.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` | pinned at implementation |

`<TargetFramework>net10.0</TargetFramework>`, `<AzureFunctionsVersion>v4</AzureFunctionsVersion>`,
`<OutputType>Exe</OutputType>`. The isolated worker is the only option — the
in-process model retires 2026-11-10 and .NET 10 never supported it. .NET isolated
does not use extension bundles.

Exact versions are re-verified against the restored packages during
implementation; the numbers above are the starting pins, not gospel.

### The sample surface

The external system is stubbed behind `IAgentDispatcher`; nothing here knows what
an agent or a GitHub issue is.

| Function | Type | Role |
|---|---|---|
| `RunWebhookTrigger` | HTTP POST + `[DurableClient]` | Starts `AgentRunOrchestrator` with a **deterministic instance ID derived from the payload**, so a duplicate webhook delivery is idempotent instead of starting a second run. Returns `CreateCheckStatusResponseAsync`. |
| `AgentRunOrchestrator` | `[OrchestrationTrigger]` | Fans out one `AgentTaskOrchestrator` sub-orchestration per work item, `Task.WhenAll`, aggregates, calls `PublishSummaryActivity`. |
| `AgentTaskOrchestrator` | `[OrchestrationTrigger]` | Calls `DispatchAgentActivity`, then races `WaitForExternalEvent<AgentResult>` against `CreateTimer` — the callback-or-timeout core. Signals `RunCounterEntity` on each outcome. |
| `CallbackTrigger` | HTTP POST + `[DurableClient]` | `RaiseEventAsync` — how the outside world wakes a parked orchestration. |
| `DispatchAgentActivity` | `[ActivityTrigger]` | Where real I/O is *allowed*. Calls `IAgentDispatcher`. |
| `PublishSummaryActivity` | `[ActivityTrigger]` | Calls `IResultPublisher`. |
| `RunCounterEntity` | `[EntityTrigger]` | `TaskEntity<RunCounterState>` tracking dispatched/completed/timed-out. |
| `RunStatusTrigger` | HTTP GET + `[DurableClient]` | Reads entity state via `client.Entities`. |

`Core` ships `IAgentDispatcher` and `IResultPublisher` with stub implementations
registered in `AddCoreServices()` — which the existing `DiRegistrationTests`
already forces (every public Core interface must be wired).

Event names are `const string` on the orchestrator, not literals at call sites.

## Guardrails

### Microsoft's analyzers, pinned

The `DURABLE*` analyzers are **bundled into
`Microsoft.Azure.Functions.Worker.Extensions.DurableTask` v1.6.0+ and on by
default** — no extra package reference. (`Microsoft.DurableTask.Analyzers` is for
the standalone SDK, not Functions. Do not add it.)

They ship at **Warning**, which `TreatWarningsAsErrors=true` already promotes to
build-breaking. `.editorconfig` pins them to `error` anyway so the intent is
explicit and survives someone relaxing that flag:

- `DURABLE0001` `DateTime.Now/UtcNow` → `context.CurrentUtcDateTime`
- `DURABLE0002` `Guid.NewGuid()` → `context.NewGuid()`
- `DURABLE0003` `Task.Delay`/`Thread.Sleep` → `context.CreateTimer`
- `DURABLE0004` `Task.Run`/`ContinueWith`/`StartNew`
- `DURABLE0005` I/O APIs in orchestrators
- `DURABLE0006` `System.Environment`
- `DURABLE0007` `CancellationToken` in orchestrator signatures
- `DURABLE0008` non-`[OrchestrationTrigger]` bindings on orchestrator params
- `DURABLE0010` non-replay-safe `ILogger` → `context.CreateReplaySafeLogger()`
- `DURABLE0011` unbounded `while(true)` without `ContinueAsNew`
- `DURABLE2003`/`DURABLE2004` unresolved activity / sub-orchestration name —
  **promoted from Info to error**

`DURABLE0009` (prefer input param over `context.GetInput<T>()`) stays at its
default; it is a style preference, not a correctness rule.
`DURABLE1001`–`1003` are already Error by default.

### Three new custom analyzers (CI0016–CI0018)

CI0001–CI0015 are taken; durable rules start at CI0016. All three are scoped to
methods with an `[OrchestrationTrigger]` parameter. **Activities and entities are
deliberately exempt** — activities are exactly where I/O belongs, and entity
operations are not replayed.

- **CI0016 `OrchestratorNonDurableAwait`** — the headline rule. Every `await` in an
  orchestrator must resolve to a `TaskOrchestrationContext` call (including its
  extension methods, via `IMethodSymbol.ReducedFrom`) or an allowlist:
  `Task.WhenAll`, `Task.WhenAny`, `Task.FromResult`, `Task.CompletedTask`.
  Everything else is an error.

  **This closes a real gap.** `DURABLE0005` only detects a fixed list of known I/O
  types (`HttpClient`, Azure Storage clients). An `await _gitHubClient.PostCommentAsync()`
  in an orchestrator is invisible to it and silently corrupts replay — the exact
  mistake the driving use case invites. CI0016 catches it regardless of type,
  because it allowlists what is *safe* rather than denylisting what is known-unsafe.

- **CI0017 `OrchestratorDependency`** — a class declaring an `[OrchestrationTrigger]`
  method may not have instance fields or constructor parameters. Catches the
  *synchronous* leak CI0016 cannot see (`_dbContext.SaveChanges()`), and the
  arbitrary-I/O-wrapper case that `DURABLE0005`'s fixed list misses.

- **CI0018 `DurableFunctionNameLiteral`** — `CallActivityAsync` and
  `CallSubOrchestratorAsync` must take `nameof(X)`, never a bare string literal.
  Makes renames safe. `DURABLE2003`/`2004` only report Info and only when they can
  resolve the name.

**Known risk:** CI0016 is the hardest of the three (extension-method receiver
resolution; `Task.WhenAll` over a `List<Task<T>>` built from context calls must stay
legal). The fan-out sample is the false-positive canary — if CI0016 fires on the
sample's own `Task.WhenAll`, the rule is wrong, not the sample. Its analyzer test
fixture must cover the fan-out shape explicitly.

### New architecture rule

A `DurableFunctionTests` fixture alongside the five copied ones. The central rule:

> **Orchestrations may see data, never behavior.** Types in
> `*.Functions.Orchestrations` may depend on `*.Core.Models` but **not** on
> `*.Core.Interfaces` or `*.Core.Services`.

This is the structural teeth: an orchestrator physically cannot reach a service in
order to call one. It complements rather than duplicates CI0017 — CI0017 bans the
injection, this bans the reference. Both must hold.

Plus: each trigger attribute is confined to its own namespace
(`[OrchestrationTrigger]` only under `Orchestrations/`, `[ActivityTrigger]` only
under `Activities/`, `[EntityTrigger]` only under `Entities/`, `[HttpTrigger]` only
under `Triggers/`).

This is why `Core` splits `Models/` from `Interfaces/`+`Services/`.

## Testing

The repo uses **NUnit + NSubstitute + FluentAssertions**. Microsoft's durable
unit-testing docs are written for **xUnit + Moq** and must be translated, not
copied.

- **`Tests.Unit`** — orchestrator logic against a substituted
  `TaskOrchestrationContext` (an abstract class, so NSubstitute can substitute it).

  **The gotcha the template must encode:** `CallActivityAsync` does not take a
  `string` — it takes a **`TaskName` struct** with an implicit conversion from
  string. So the call site reads like a string but the substitution must match
  `Arg.Is<TaskName>(n => n.Name == nameof(DispatchAgentActivity))`, with
  `Arg.Any<TaskOptions>()` for the options parameter. Matching a raw string does
  not work. Getting this wrong is the most common way durable unit tests fail
  confusingly, so the sample tests demonstrate the correct form rather than
  leaving the forker to discover it.

  **Corrected 2026-07-15 during Task 5.** An earlier draft of this spec claimed
  the convenient overloads were "extension methods and cannot be substituted."
  That is false, and it came from an unverified research claim. Reflection over
  `Microsoft.DurableTask.Abstractions` 1.24.1 shows `TaskOrchestrationContext`
  declares all four `CallActivityAsync` overloads **on the type itself**, all
  `virtual` (the three-argument generic one additionally `abstract`) — so
  NSubstitute handles every one of them, and no hand-written test double is
  needed. The `TaskName` matching requirement above is the real constraint and is
  the only one the template should teach.

  Covers: the timer-vs-event race (both branches), fan-out aggregation, and the
  deterministic instance ID.

- **`Tests.Integration`** — the honest one. Boots the Functions host against
  Azurite, posts to the webhook trigger, posts a callback, asserts the
  orchestration reaches `Completed` with the expected output; and asserts the
  timeout path completes without a callback. This is the only thing that proves
  replay actually works.

  Requires Azurite on ports 10000/10001/**10002** (durable needs tables) and
  Functions Core Tools **v4** (v5 is preview and not at feature parity — stay on
  v4). Both install via npm on `windows-latest` with no Docker.

  **Accepted cost:** a developer running `dotnet test` at solution root without
  Azurite gets failures. The pre-commit gate is unaffected — it filters to
  `*Unit*` and `*Arch*` projects only, so commits never depend on Azurite. The
  README documents the requirement and `setup.ps1` checks for Azurite and prints
  actionable guidance if absent (checks, does not install — no silent npm installs).

  **Known risk:** host startup in a test fixture is the novel piece and is the most
  likely thing to be flaky. If driving `func start` from a `OneTimeSetUp` proves
  unreliable, the fallback is to keep the integration project's Azurite-dependent
  tests behind an explicit category and run them only in CI. Fall back rather than
  ship a test that skips silently and reads as passing.

- **`Tests.Analyzers`** — three new fixtures, one per new rule. CI0016's must
  include the fan-out false-positive cases.

- **`Tests.Architecture`** — the five copied fixtures plus `DurableFunctionTests`.

## Ecosystem integration

- **`plugin/scripts/write-stamp.ps1`:** add `durable` to the `-Template`
  `ValidateSet` (currently `'cli','etl-api','expo-app','mcp'`) and
  `'durable' = 'dotnet/templates/durable'` to `$templateDirs`.
- **`new-project/handlers/dotnet-durable.ps1`:** new handler — Type
  `dotnet-durable`, StampName `durable`, no `ExtraArgs`, PreInstall
  `dotnet new install "$($ctx.Repo)/dotnet" --force`, Scaffold
  `dotnet new durable -n $ctx.Name -o $ctx.Dest`, Verify `dotnet build
  --no-incremental` + `dotnet test`. Mirrors `dotnet-mcp.ps1` exactly.
- **`new-project/tests/lib-tests.ps1`:** the discovery assertion is an exact
  string — `'dotnet-cli,dotnet-etl-api,dotnet-mcp,expo'` becomes
  `'dotnet-cli,dotnet-durable,dotnet-etl-api,dotnet-mcp,expo'` (alphabetical), and
  "four types" becomes "five types". The usage loop gains `dotnet-durable`.
- **`plugin/skills/new-dotnet-durable/SKILL.md`:** new thin delegate skill matching
  the other four; delegates to `new-project.ps1 dotnet-durable`.
- **`plugin/template-tests/validate-plugin.ps1`:** add `new-dotnet-durable` to the
  `-RequireFull` required-skills list.
- **CI (`.github/workflows/template-ci.yml`):** add `durable` to the
  `scaffold-and-test` matrix (`[cli, etl-api, mcp]` → `[cli, durable, etl-api,
  mcp]`), plus an Azurite + Core Tools install step. The step must be conditional
  on the matrix template (`if: matrix.template == 'durable'`) so the other three
  templates do not pay for it.
- **`dotnet/README.md`:** add `durable` to the template table. Note the table
  currently says "The three templates" and "13 custom Roslyn analyzers
  (CI0001–CI0013)" — both are already stale (there are 15 analyzers and 4
  templates as of the mcp addition). Correct these while editing rather than
  adding a fourth wrong number.
- **Root `README.md`:** add `durable` to the harnesses table.
- **`harness-manifest.json`:** owns the same paths as mcp's, plus `host.json`.

## Success criteria

- `dotnet new install ./dotnet` then `dotnet new durable -n Foo` produces a
  solution where `dotnet build --no-incremental`, `dotnet format
  --verify-no-changes`, and `dotnet test` all pass with every analyzer and
  architecture rule green.
- The integration test proves an orchestration started by a webhook, parked on an
  external event, resumed by a callback, and completed — against a real Azurite
  backend.
- Seeding each violation fails the build: `DateTime.UtcNow` in an orchestrator
  (DURABLE0001), `await _dispatcher.DispatchAsync()` in an orchestrator (CI0016),
  a constructor dependency on an orchestrator class (CI0017), a string-literal
  activity name (CI0018), and an `Orchestrations/` type referencing
  `Core.Interfaces` (architecture test).
- CI0016 does **not** fire on the fan-out sample's `Task.WhenAll`.
- `new-project dotnet-durable Foo` scaffolds, stamps (`template=durable`), sets up,
  and verifies end-to-end.
- The `new-dotnet-durable` skill, CI matrix entry, and plugin validation all
  recognize the new type.
