# dotnet-agent-harness

`dotnet new` templates designed for letting an AI agent — Claude Code, Codex, or anything similar — write the code.

## Why this exists

A vanilla `dotnet new console` project gives an agent a lot of rope. There are no guardrails, so the predictable failure modes go undetected:

- **Drift.** The agent leaves comments behind. Methods grow to 80 lines. Constructors take 9 dependencies. Public types get nested. Tuples get returned from public APIs. Serialised anonymous objects appear in HTTP handlers. None of this fails the build, so the agent doesn't notice and the next session inherits the mess.
- **Lifecycle bypass.** The agent commits straight to `main`, forgets to open an issue, edits a branch that's already been merged, or skips tests because "the change is small."
- **Architectural decay.** The agent registers a service but forgets the interface in DI. It puts a `DbContext` reference in `Core.Models`. It makes Api depend on Etl. The build still passes; nothing complains until weeks later when something breaks in production.
- **Style entropy.** Allman vs K&R braces. `_camelCase` vs `m_camelCase`. CRLF vs LF. The agent picks whichever matches the last file it read, and the codebase smears.

These templates ship the guardrails pre-installed, set to **error severity**. The agent finds out about every one of these the moment it tries to compile or commit, not in code review three days later. In practice that means you can let the agent work for longer between human checkpoints, because the harness catches the things you'd otherwise have to catch yourself.

## What's in the box

Every scaffolded project includes:

- **A `CLAUDE.md`** that documents the lifecycle (issue → branch → commit → PR) and the code style (no comments, no tuple returns, etc.) so the agent reads the rules on session start.
- **Three git/Claude hooks** — `pre-commit` (runs `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test` and refuses the commit if any fails), `reference-transaction` (rejects branch creation unless the name matches `feat/<issue#>-<slug>` and the issue exists on GitHub), and `block-merged-branch` (a Claude Code `PreToolUse` hook that blocks edits to branches that have already been squash-merged).
- **13 custom Roslyn analyzers** (CI0001–CI0013) that fire at error severity: no comments, method length cap, constructor parameter cap, no tuple returns, no anonymous serialization, no `pragma warning disable`, no chained `null` in arguments, no `Assert.Ignore`, no `IsNotNull`-only assertions, public method count cap, public type per file, test fixture must exist for every public class, etc.
- **5 architecture-test fixtures** (NetArchTest + reflection) that fail the test run if the agent breaks layering, naming, DI shape, DI registration (every public `Core` interface must be wired in `AddCoreServices()`), or one-public-type-per-file.
- **`Directory.Build.props`** with `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-all`, and `EnforceCodeStyleInBuild=true`. Any CA / IDE / CS / CI diagnostic at error severity breaks the build.

## The two templates

| Short name | Shape | When to pick it |
|---|---|---|
| `cli` | Single console entry point (`Cli`) sharing `Core` + `Analyzers`. 3 src + 4 test projects. | One executable. Background workers, CLI tools, ETL jobs, any agent task that doesn't need an HTTP surface. |
| `etl-api` | Two entry points (`Api` + `Etl`) sharing `Core` + `Analyzers`. 4 src + 4 test projects. | An HTTP API plus a long-running worker, sharing domain logic via `Core`. The `LayerDependencyTests` enforce that they never reference each other. |

Pick `cli` by default. Move to `etl-api` only when you actually need both surfaces.

## Install

`dotnet new install` doesn't accept git URLs — clone first, then install from the local repo root:

```powershell
git clone https://github.com/ryan75195/dotnet-agent-harness
dotnet new install .\dotnet-agent-harness
```

Both templates register at once. Verify:

```powershell
dotnet new list cli
dotnet new list etl-api
```

## Use

```powershell
dotnet new cli -n MyTool       # or: dotnet new etl-api -n MyPlatform
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
dotnet new install . --force
```

## Uninstall

```powershell
dotnet new uninstall <path-you-used-at-install-time>
```

## Repo layout

```
dotnet-agent-harness/
  README.md                              ← this file
  .github/workflows/template-ci.yml      ← CI: scaffold + build + test both templates on every push
  template-tests/scaffold-and-build.ps1  ← local validation: .\template-tests\scaffold-and-build.ps1 cli
  templates/
    cli/
      .template.config/template.json     ← template manifest
      src/, tests/, .githooks/, .claude/, CLAUDE.md, ...
    etl-api/
      .template.config/template.json
      src/, tests/, .githooks/, .claude/, CLAUDE.md, ...
```

Each template directory under `templates/` is a self-contained `dotnet new` template. `dotnet new install <harness-root>` recursively scans for `.template.config/template.json` and registers both.

## Development

This harness repo doesn't have its own pre-commit hooks (those only fire in scaffolded projects). The CI workflow is the safety net — it scaffolds + builds + tests both templates on every push. To verify changes locally before pushing:

```powershell
.\template-tests\scaffold-and-build.ps1 cli
.\template-tests\scaffold-and-build.ps1 etl-api
```

Each invocation installs the chosen template, scaffolds a smoke project into `$env:TEMP`, builds it, runs all four test projects, and uninstalls.
