# SampleDurable

Durable Functions solution scaffolded from the [dotnet-agent-harness](https://github.com/ryan75195/dotnet-agent-harness) `durable` template.

## What this is

An Azure Functions **isolated worker** host that orchestrates work with the Durable Functions
extension. It ships with one webhook-driven workflow to fork from: a run starts over HTTP, fans
out into parallel sub-orchestrations, each of which dispatches a unit of work and waits for either
a callback or a timeout, and a durable entity tracks progress counters throughout.

- **`RunWebhookTrigger`** (`POST /api/runs`) — starts an `AgentRunOrchestrator` instance keyed by
  a deterministic ID (`run-{RunKey}`), so retried/duplicate webhook deliveries return the existing
  run instead of starting a second one.
- **`AgentRunOrchestrator`** (`src/SampleDurable.Functions/Orchestrations/`) — fans out one
  `AgentTaskOrchestrator` sub-orchestration per work item (each with its own deterministic
  sub-instance ID), waits for all of them with `Task.WhenAll`, and publishes a summary through an
  activity.
- **`AgentTaskOrchestrator`** — dispatches its work item through the `DispatchAgentActivity`
  activity, then races `context.WaitForExternalEvent` against a durable timer with
  `Task.WhenAny`. Whichever finishes first decides the outcome.
- **`CallbackTrigger`** (`POST /api/runs/{instanceId}/callback`) — raises the external event that
  wins that race, so an external system (or a human) reports completion back into the run.
- **`RunCounterEntity`** — a durable entity that tallies dispatched/completed/timed-out counts as
  the run progresses.
- **`RunStatusTrigger`** (`GET /api/runs/{instanceId}/counters`) — reads the entity's current
  state back out.

Business logic — anything that actually touches the outside world — lives in `Core` services
(`src/SampleDurable.Core/Services/`) behind interfaces in `Core/Interfaces/`, called from
activities. Orchestrators never call a `Core` service directly; they only call activities and
sub-orchestrations by name, and this is enforced structurally, not just by convention (see
[CLAUDE.md](./CLAUDE.md) for why).

## First-time setup

After scaffolding (`dotnet new durable -n SampleDurable`), run once:

```powershell
.\setup.ps1
```

This initializes a git repo, activates `.githooks/` for the project lifecycle, and creates the initial commit.

## Build and test

```powershell
dotnet restore
dotnet build
dotnet test
```

`dotnet test` runs all four test projects. `Tests.Unit`, `Tests.Architecture`, and
`Tests.Analyzers` need nothing extra. `Tests.Integration` needs Azurite and the Functions host
running — see below.

## Run locally

Durable Functions persists orchestration state in storage. This template defaults to Azure
Storage, emulated locally by Azurite:

```powershell
npm install -g azurite
azurite --silent --inMemoryPersistence --skipApiVersionCheck
```

Azurite must be serving blob (10000), queue (10001), **and table (10002)** — durable state lives
in tables, so it's easy to think the emulator is ready when only blob/queue are actually up.
`--inMemoryPersistence` and `--location` are mutually exclusive; don't pass both.

Then, from `src/SampleDurable.Functions`:

```powershell
func start
```

Do not add `--csharp` — it forces the in-process worker model and silently overrides this
template's isolated-worker configuration. An npm-installed Azure Functions Core Tools works
fine; nothing extra is needed to put `func` on `PATH` for the integration tests either.

Once running, start a workflow:

```powershell
curl -X POST http://localhost:7071/api/runs -H "Content-Type: application/json" `
  -d '{"runKey":"demo","items":[{"id":"item-1","prompt":"say hello"}]}'
```

then complete a work item's callback (substitute the `instanceId` the previous call returned):

```powershell
curl -X POST http://localhost:7071/api/runs/<instanceId>/callback -H "Content-Type: application/json" `
  -d '{"workItemId":"item-1","succeeded":true,"output":"done"}'
```

and check progress:

```powershell
curl http://localhost:7071/api/runs/<instanceId>/counters
```

## Guardrails

Beyond the 15 analyzers shared with the other templates in this repo (method length, constructor
shape, no comments, no tuple returns, and so on), this template adds three durable-specific
rules, all at error severity:

| Rule | Blocks | Fix |
|---|---|---|
| CI0016 | An orchestrator `await`s something that isn't a durable operation | Use the durable equivalent (`context.CreateTimer`, `context.CallActivityAsync`, ...), or move the work into an activity |
| CI0017 | An orchestrator class declares instance state (fields, a constructor) | Keep orchestrators `static`; pass data through parameters and the durable context instead |
| CI0018 | `CallActivityAsync`/`CallSubOrchestratorAsync` called with a string literal instead of `nameof(...)` | Always call with `nameof(TheActivityOrOrchestrator)` |

Microsoft's own `DURABLE*` analyzers (determinism rules like banning `DateTime.UtcNow` and
`Guid.NewGuid()` in orchestrators) ship inside the `Microsoft.Azure.Functions.Worker.Extensions.DurableTask`
package already referenced by `Functions` — there's no extra package to install, and installing
`Microsoft.DurableTask.Analyzers` would conflict with it (that package targets the standalone
Durable Task SDK, not the Functions worker extension).

`tests/SampleDurable.Tests.Architecture` adds a sixth fixture, `DurableFunctionTests`, on top of
the five shared with the other templates: it asserts orchestrations depend on `Core.Models` but
never on `Core.Interfaces`/`Core.Services`, and that every trigger type lives in its matching
folder.

## Switching to the Durable Task Scheduler

Azure Storage is the default backend and needs no extra packages — it's what this template ships
with. Microsoft recommends the **Durable Task Scheduler (DTS)** for new production apps instead.
To switch:

1. Add `Microsoft.Azure.Functions.Worker.Extensions.DurableTask.AzureManaged`.
2. Set `storageProvider.type` to `azureManaged` in `host.json`.
3. Point the connection string at your scheduler instance. DTS is managed-identity only — there
   are no connection keys to configure.

The template doesn't default to DTS because its local emulator
(`mcr.microsoft.com/dts/dts-emulator`, dashboard on `:8082`) runs as a Docker container. Defaulting
to it would put Docker on the critical path for `setup.ps1` and for CI — and this repo's CI runs
on Windows runners, where GitHub Actions does not support service containers at all. Azure
Storage needs nothing beyond Azurite (a plain npm package), which is why it's the default; adopt
DTS once you're ready to run it against a real scheduler instance, or if you're comfortable adding
Docker to your local and CI setup for the emulator.

## Development lifecycle

See [CLAUDE.md](./CLAUDE.md) for the full lifecycle (issue → branch → commit → PR).

Quick summary:
1. `gh issue create --title "..."` (every change starts with an issue)
2. `git checkout -b feat/<issue-num>-<slug>` (`reference-transaction` hook verifies the issue exists)
3. Edit + commit (pre-commit hook runs build, format, and the Unit + Architecture tests)
4. `gh pr create` and squash-merge

Direct commits to `main` are blocked. Edits to already-merged branches are blocked.
