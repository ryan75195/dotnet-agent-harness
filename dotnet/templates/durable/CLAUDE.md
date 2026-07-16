# CLAUDE.md

Project context for Claude Code sessions. Read this before making changes.

## Development lifecycle

Every change follows this loop. None of these steps are optional — hooks enforce each transition.

1. **Open an issue.** `gh issue create --title "..."`. No issue, no branch.
2. **Create a feat branch.** `git checkout -b feat/<N>-<kebab-slug>` where `<N>` is the issue number. `.githooks/reference-transaction` rejects the branch on creation if the name doesn't match or if issue #N doesn't exist on GitHub.
3. **Edit + test.** Run *targeted* tests while iterating (`dotnet test <project>`) for fast feedback. Don't run the full build/test gate just to commit — the pre-commit hook (step 4) runs it and blocks on failure, so committing *is* running the tests, and a blocked commit is also what feeds harness feedback capture. Analyzers run on every build.
4. **Commit.** `git commit`. `.githooks/pre-commit` runs:
   - Branch guard (no commits to `main`/`master`)
   - Merged-branch check (`.claude/hooks/block-merged-branch.sh`)
   - `dotnet build --no-incremental` — all analyzers at error severity block
   - `dotnet format --verify-no-changes` — style, encoding, line endings
   - `dotnet test` — only the `Tests.Unit` and `Tests.Architecture` projects (the hook matches
     paths containing `Unit` or `Arch`; `Tests.Analyzers` and `Tests.Integration` are not
     matched and do not run at commit time). CI runs all four, including Integration against a
     real Azurite instance and Functions host.
5. **Open PR.** `gh pr create --base main --head feat/<N>-<slug>`.
6. **Squash merge.** `gh pr merge <N> --squash --delete-branch`.
7. **Capture review feedback.** After the merge, invoke the
   `agent-harness:harness-capture-review` skill for PR `<N>` to record any
   human review comments for later rule synthesis (best-effort; never
   blocks).

**Direct edits and commits to `main` are blocked.** **Edits to an already-merged branch are blocked** (Claude Code `PreToolUse` hook + pre-commit).

## Code style

- **No comments.** `NoCommentsAnalyzer` (CI0013) blocks `//`, `/* */`, and `///` at error severity. Extract intent into method names, variable names, or types. If a WHY is genuinely non-obvious (hidden constraint, bug workaround), extract it into a named helper — don't write a comment.
- **`TreatWarningsAsErrors=true`** with `AnalysisLevel=latest-all`. Any CA/IDE/CS/CI diagnostic at severity `error` breaks the build. Exception: NU1510 shows as a non-blocking warning.
- Full style rules live in `.editorconfig` (root + `tests/` override). Nullable reference types enabled everywhere. File-scoped namespaces, Allman braces, `_camelCase` private fields, `I`-prefixed interfaces.

## Architecture

- **Solution:** `SampleDurable.slnx`, .NET 10, three `src/` projects (`Core`, `Functions`, `Analyzers`) and four `tests/` projects (`Tests.Unit`, `Tests.Integration`, `Tests.Architecture`, `Tests.Analyzers`).
- **`Functions`** is an Azure Functions **isolated worker** host (`Program.cs` builds a plain `HostBuilder` with `.ConfigureFunctionsWebApplication()` — deliberate, not an oversight: `FunctionsApplication.CreateBuilder` doesn't resolve against the pinned SDK version). Orchestrations live under `Orchestrations/` (`[OrchestrationTrigger]`), activities under `Activities/` (`[ActivityTrigger]`), entities under `Entities/` (`[EntityTrigger]`), and HTTP triggers under `Triggers/` (`[HttpTrigger]` + `[DurableClient]`). Functions are discovered by attribute — no manual registration.
- **Every function entry point is named `Run`, never `RunAsync`.** `NamingConventionTests` bans the `Async` suffix on methods the template declares (framework overrides like `ExecuteAsync` are exempted), and `[Function(name)]` carries the real dispatch name anyway — the method name is just a label. This is also the idiom the Azure Functions templates themselves use.
- **The sample:** `RunWebhookTrigger` (`POST /api/runs`) starts `AgentRunOrchestrator` with a deterministic instance ID `run-{RunKey}`, so a duplicate webhook delivery returns the existing run instead of starting a second one. The orchestrator fans out one `AgentTaskOrchestrator` sub-orchestration per work item — each with its own deterministic sub-instance ID from `AgentRunOrchestrator.SubInstanceId` — `Task.WhenAll`s them, and publishes a summary via an activity. Each `AgentTaskOrchestrator` dispatches the work item through an activity, then races `context.WaitForExternalEvent` against a durable timer with `Task.WhenAny`; `CallbackTrigger` (`POST /api/runs/{instanceId}/callback`) raises the event that wins the race. `RunCounterEntity` tallies dispatched/completed/timed-out counts as the run progresses; `RunStatusTrigger` (`GET /api/runs/{instanceId}/counters`) reads it back.
- **Orchestrators must be deterministic.** They are replayed from an event history on every step — the same code runs many times, and must produce the same result each time it does. This is not a style preference: violating it corrupts running workflows in ways that do not reproduce locally, only in production, often much later. Concretely: no `DateTime.UtcNow` (use `context.CurrentUtcDateTime`), no `Guid.NewGuid()` (use `context.NewGuid()`), no `Task.Delay` (use `context.CreateTimer`), no I/O, no injected services. **This is enforced at error severity, not by convention** — see the guardrail table below.
- **Where work belongs:** anything touching the outside world (an HTTP call, a queue, a database, a clock, randomness) goes in an **activity**, which calls a `Core` service to do it. Orchestrators only decide *what* to call and in *what order*. Activities may inject dependencies through their constructor and accept a `CancellationToken`; orchestrators may do neither — both are means for an orchestrator to reach outside its own replay-safe world.
- **`Core`** splits `Models/` (DTOs — safe for an orchestrator to reference) from `Interfaces/` and `Services/` (behaviour — never safe). Orchestrations may depend on `Core.Models` but **not** on `Core.Interfaces`/`Core.Services`; `DurableFunctionTests` enforces this structurally by asserting the `Orchestrations` namespace has no dependency on either. This split is load-bearing, not cosmetic: the orchestrator classes are `static`, so they hold no instance state for CI0017 to catch, and a static orchestrator that reaches a service through a local variable with no `await` is invisible to CI0016 too (it only inspects `await` expressions). The namespace-dependency rule is the only guardrail that would catch that case.
- **Architecture tests** in `tests/SampleDurable.Tests.Architecture/` enforce layering, DI shape, DI wiring (every public Core interface must be registered via `AddCoreServices()`), naming, one-public-type-per-file, and the durable-specific rules above (`DurableFunctionTests`).
- **Custom analyzers** in `src/SampleDurable.Analyzers/` enforce CI0001–CI0018: the 15 shared with the other templates (method length, ctor param count, no tuple returns, no anonymous serialization, no comments, etc.) plus three durable-only rules:
  - **CI0016** — an orchestrator method may only `await` a durable operation (something on `TaskOrchestrationContext`/`Microsoft.DurableTask.*`), never a raw `Task`. **Wants instead:** call the durable equivalent (`context.CreateTimer` instead of `Task.Delay`, `context.CallActivityAsync` instead of calling a service directly) or move the non-durable work into an activity.
  - **CI0017** — an orchestrator class may hold no instance state (no fields, no constructor). **Wants instead:** keep orchestrators `static`, and pass anything they need as method parameters or through the durable context.
  - **CI0018** — `CallActivityAsync`/`CallSubOrchestratorAsync` must be called with `nameof(...)`, never a string literal. **Wants instead:** `context.CallActivityAsync(nameof(DispatchAgentActivity), ...)`, so a rename of the activity/orchestrator class is a compile error at the call site instead of a silent runtime mismatch.
- **Microsoft's `DURABLE*` analyzers** ship bundled inside `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` — there is no separate package to add, and specifically **do not add `Microsoft.DurableTask.Analyzers`**; that package targets the standalone Durable Task SDK (not the Functions worker extension) and conflicts with it. They're pinned to `error` severity in `.editorconfig`.

## Running locally

Durable Functions needs a storage backend to persist orchestration state and history. This
template uses Azure Storage by default, emulated locally by **Azurite**:

```powershell
npm install -g azurite
azurite --silent --inMemoryPersistence --skipApiVersionCheck
```

Azurite must be serving all three of blob (10000), queue (10001), **and table (10002)** —
durable state lives in the table service, so a check (or an Azurite invocation) that only
confirms blob is up will pass while the orchestrator storage is still missing. `--skipApiVersionCheck`
guards against a version mismatch between the installed Azurite and the pinned Durable Task SDK.
Do not also pass `--location` — it and `--inMemoryPersistence` are mutually exclusive, and
Azurite exits immediately if given both.

With Azurite running, start the Functions host from `src/SampleDurable.Functions`:

```powershell
func start
```

**Do not pass `--csharp`** — it forces the in-process worker model and silently overrides this
template's `dotnet-isolated` setting (from `local.settings.json`), producing confusing failures
that look unrelated to the flag. An npm-installed Core Tools works fine here; the integration
test fixture (`FunctionHostFixture`) resolves `func.cmd` off `PATH` itself, so no MSI install or
manual `PATH` fix-up is needed.

None of this is required for `Tests.Unit`, `Tests.Architecture`, or `Tests.Analyzers` — those,
and only those, are what the pre-commit hook runs, so **committing never requires Azurite**.
Only `Tests.Integration` needs the emulator and the host running.

## Testing judgment calls

The toolchain enforces coverage (CI0002), assertion quality (CI0009), fixture existence, and naming. What it cannot enforce is *what kind* of test to write and *what* to assert on.

**Unit vs integration:** a unit test mocks boundaries; an integration test crosses them. Decide based on: *if the mock were wrong, would you ship a bug?* Entry points (endpoints, background services) and persistence (database writes) almost always need integration tests — a mock can't verify routing, schema, serialization wire format, or transaction behavior. Everything else: unit test with mocks is usually sufficient.

**What to assert:** assert on the observable outcome the feature promises, not on how it gets there internally. Query → returned data matches expectations. Command → side effect occurred (DB state changed, event published). Transformation → output structure and values are correct. If you're unsure, ask: "what would a user notice if this broke?"

**When a mock is dangerous:** a mock is an assumption about how something else behaves. Mocking a stable in-process interface (ILogger, ITimeProvider) is always safe. Mocking a boundary where the contract can drift without your code changing (database queries, serialization formats, HTTP integrations) is where bugs hide. When the mock's fidelity matters to correctness, cross the boundary in a test instead.

## Key files

- `.githooks/pre-commit` — commit-time enforcement
- `.githooks/reference-transaction` — branch-creation enforcement
- `.claude/hooks/block-main-branch.sh` — edit-time main/master protection
- `.claude/hooks/block-merged-branch.sh` — shared merged-branch check
- `.claude/settings.json` — Claude Code hook registration
- `Directory.Build.props` — `TreatWarningsAsErrors`, analyzer project wire-up
- `.editorconfig` + `tests/.editorconfig` — style + severity overrides

## Harness maintenance

This project was scaffolded from the agent-harness template repo.
`.harness.json` records the template and commit it came from — never
hand-edit or delete it.

- **Updating the harness.** On any request like "update the harness",
  "pull the latest template changes", or "update the guardrails", invoke
  the `agent-harness:harness-update` skill (ships with the agent-harness
  plugin). It updates only harness-owned files — hooks, lint rules,
  analyzers, CI, this file — never your app code, and goes through the
  normal issue → branch → PR lifecycle.
- **Feedback events.** When a blocked commit prints
  `HARNESS-FEEDBACK: event <id>`, append a one-line note describing what
  the failing code was trying to do (the agent-harness plugin injects the
  exact `harness-note.ps1` command), then fix the failure and commit
  again as normal.
