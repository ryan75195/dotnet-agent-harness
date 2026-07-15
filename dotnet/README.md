# dotnet-agent-harness

`dotnet new` templates designed for letting an AI agent — Claude Code, Codex, or anything similar — write the code.

## Why this exists

A vanilla `dotnet new console` project gives an agent a lot of rope. There are no guardrails, so the predictable failure modes go undetected:

- **Drift.** The agent leaves comments behind. Methods grow to 80 lines. Constructors take 9 dependencies. Public types get nested. Tuples get returned from public APIs. Serialised anonymous objects appear in HTTP handlers. None of this fails the build, so the agent doesn't notice and the next session inherits the mess.
- **Lifecycle bypass.** The agent commits straight to `main`, forgets to open an issue, edits a branch that's already been merged, or skips tests because "the change is small."
- **Architectural decay.** The agent registers a service but forgets the interface in DI. It puts a `DbContext` reference in `Core.Models`. It makes Api depend on Etl. The build still passes; nothing complains until weeks later when something breaks in production.
- **Style entropy.** Allman vs K&R braces. `_camelCase` vs `m_camelCase`. CRLF vs LF. The agent picks whichever matches the last file it read, and the codebase smears.

You can write all of this down in `CLAUDE.md`, `AGENTS.md`, a system prompt, or a slash command, and the agent will sometimes still ignore it. Soft rules in natural language don't reliably survive a long session — the agent forgets, reprioritises, or convinces itself the current case is an exception. The only reliable way to enforce an architectural rule on an agent is to make it **stop the build**: a failing diagnostic, a failing test, a rejected commit. The agent reads the error, understands what broke, and self-corrects. That's a feedback loop the agent can't talk its way out of.

These templates encode the rules that way. Every guardrail is wired into the toolchain at **error severity** — the agent finds out the moment it tries to compile or commit, not in code review three days later. In practice that means you can let the agent work for longer between human checkpoints, because the harness catches the things a soft prompt wouldn't.

## What's in the box

Every scaffolded project includes:

- **A `CLAUDE.md`** that documents the lifecycle (issue → branch → commit → PR) and the code style (no comments, no tuple returns, etc.) so the agent reads the rules on session start.
- **Four git/Claude hooks** — `pre-commit` (runs `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test` and refuses the commit if any fails), `reference-transaction` (rejects branch creation unless the name matches `feat/<issue#>-<slug>` and the issue exists on GitHub), `block-main-branch` (a Claude Code `PreToolUse` hook that blocks edits on `main`/`master`), and `block-merged-branch` (a Claude Code `PreToolUse` hook that blocks edits to branches that have already been squash-merged).
- **15 custom Roslyn analyzers shared by every template** (CI0001–CI0015) that fire at error severity: no comments, method length cap, constructor parameter cap, no tuple returns, no anonymous serialization, no `pragma warning disable`, no chained `null` in arguments, no `Assert.Ignore`, no `IsNotNull`-only assertions, public method count cap, public type per file, test fixture must exist for every public class, etc. The `durable` template adds three more (CI0016–CI0018) that enforce orchestrator determinism, for 18 total.
- **5 architecture-test fixtures** (NetArchTest + reflection) that fail the test run if the agent breaks layering, naming, DI shape, DI registration (every public `Core` interface must be wired in `AddCoreServices()`), or one-public-type-per-file. The `durable` template adds a 6th, `DurableFunctionTests`, that confines orchestrations to `Core.Models`.
- **`Directory.Build.props`** with `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-all`, and `EnforceCodeStyleInBuild=true`. Any CA / IDE / CS / CI diagnostic at error severity breaks the build.

## The four templates

| Short name | Shape | When to pick it |
|---|---|---|
| `cli` | Single console entry point (`Cli`) sharing `Core` + `Analyzers`. 3 src + 4 test projects. | One executable. Background workers, CLI tools, ETL jobs, any agent task that doesn't need an HTTP surface. |
| `etl-api` | Two entry points (`Api` + `Etl`) sharing `Core` + `Analyzers`. 4 src + 4 test projects. | An HTTP API plus a long-running worker, sharing domain logic via `Core`. The `LayerDependencyTests` enforce that they never reference each other. |
| `mcp` | Single ASP.NET Core entry point (`Server`) serving MCP over HTTP/streamable transport, sharing `Core` + `Analyzers`. 3 src + 4 test projects. | You're building tools/resources/prompts for an MCP client (Claude, another agent) rather than a human-facing API. Ships a sample `echo` tool, `greeting` resource, and `summarize` prompt to fork from. |
| `durable` | Single Azure Functions isolated host (`Functions`) sharing `Core` + `Analyzers`. 3 src + 4 test projects. | Work that outlives a process: kick off an external system, park for hours, resume when it calls back. Ships a webhook-started orchestration with fan-out/fan-in, an external-event-vs-timer race, and a durable entity. Orchestrator determinism is enforced at error severity — Microsoft's `DURABLE*` rules plus CI0016–CI0018. |

Pick `cli` by default. Move to `etl-api` when you need both an HTTP API and a worker. Move to `mcp` when the HTTP surface is an MCP server, not a REST API. Move to `durable` when the work outlives a single process — long waits on external systems, callbacks, or workflows that must survive a crash mid-run. If a `cli` worker would need to poll a database to remember where it got to, you want `durable`.

## Install

`dotnet new install` doesn't accept git URLs — clone first, then install from the local repo root:

```powershell
git clone https://github.com/ryan75195/dotnet-agent-harness
dotnet new install .\dotnet-agent-harness\dotnet
```

All four templates register at once. Verify:

```powershell
dotnet new list cli
dotnet new list etl-api
dotnet new list mcp
dotnet new list durable
```

## Use

```powershell
dotnet new cli -n MyTool       # or: dotnet new etl-api -n MyPlatform, dotnet new mcp -n MyMcpServer, dotnet new durable -n MyWorkflow
cd MyTool
.\setup.ps1
```

`MyTool` becomes the project name everywhere — namespaces, project files, the `.slnx`. After `setup.ps1`:

- git repo is initialised on `main`
- `.githooks/` is active (so the next commit triggers all the guardrails)
- the initial scaffold is committed
- ready for `gh repo create` and the first issue

From here, point your agent at the project and let it work. The first time it tries to leave a `// TODO` behind, the build will fail with `CI0013: NoCommentsAnalyzer`, and the agent will read the diagnostic and self-correct. That's the whole point.

## Update

```powershell
cd dotnet-agent-harness
git pull
dotnet new install .\dotnet --force
```

## Uninstall

```powershell
dotnet new uninstall <path-you-used-at-install-time>
```

## Repo layout

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
      mcp/
      durable/
  expo/
    ...                                         ← Expo app template (see root README)
```

Each template directory under `templates/` is a self-contained `dotnet new` template. `dotnet new install <harness-root>` recursively scans for `.template.config/template.json` and registers all four.

## Development

This harness repo doesn't have its own pre-commit hooks (those only fire in scaffolded projects). The CI workflow is the safety net — it scaffolds + builds + tests all four templates on every push. To verify changes locally before pushing:

```powershell
.\dotnet\template-tests\scaffold-and-build.ps1 cli
.\dotnet\template-tests\scaffold-and-build.ps1 etl-api
.\dotnet\template-tests\scaffold-and-build.ps1 mcp
.\dotnet\template-tests\scaffold-and-build.ps1 durable
```

Each invocation installs the chosen template, scaffolds a smoke project into `$env:TEMP`, builds it, and uninstalls. For `cli`/`etl-api`/`mcp` it runs all four test projects. For `durable` it runs only Unit/Architecture/Analyzers — the same scope as the pre-commit hook and the `new-project` `dotnet-durable` handler's `Verify` step — because Integration needs Azurite and Azure Functions Core Tools v4 reachable, and this smoke test must pass with neither installed. To exercise Integration too, install Azurite (`npm install -g azurite`) and Core Tools (`npm install -g azure-functions-core-tools@4`), start Azurite (`azurite --silent --inMemoryPersistence --skipApiVersionCheck` — note `--inMemoryPersistence` and `--location` are mutually exclusive), and run `dotnet test` inside a scaffolded `durable` project, or push and watch the durable CI job, which exercises it on every push.
