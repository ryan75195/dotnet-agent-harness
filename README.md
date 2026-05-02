# dotnet-agent-harness

Opinionated `dotnet new` templates for agentic .NET development. Each template ships:

- A `CLAUDE.md` documenting an issue → branch → commit → PR lifecycle
- Three git/Claude hooks enforcing that lifecycle (no commits to `main`, no edits to merged branches, branch names must match a real GitHub issue)
- 13 custom Roslyn analyzers (CI0001–CI0013) blocking comments, long methods, fat constructors, anonymous serialization, etc., at error severity
- 5 architecture-test fixtures (NetArchTest + reflection) enforcing layering, naming, DI shape, and DI registration
- `Directory.Build.props` with `TreatWarningsAsErrors=true` and `AnalysisLevel=latest-all`

The shared idea: when an agent (Claude Code, Codex, etc.) works inside one of these scaffolded projects, the guardrails catch the things the agent would otherwise drift on.

## The two templates

| Short name | Shape | When to pick it |
|---|---|---|
| `cli` | Single console entry point (`Cli`) sharing `Core` + `Analyzers` (3 src + 4 test projects) | One executable. Background workers, CLI tools, ETL jobs that don't need an HTTP surface. |
| `etl-api` | Two entry points (`Api` + `Etl`) sharing `Core` + `Analyzers` (4 src + 4 test projects) | An HTTP API plus a long-running worker, sharing domain logic via `Core`. |

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

- git repo is initialised (`-b main`)
- `.githooks/` is active
- initial commit is in
- ready for `gh repo create` and the first issue

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
  docs/superpowers/                      ← design specs and implementation plans
  cli/
    .template.config/template.json       ← template manifest
    src/, tests/, .githooks/, .claude/, CLAUDE.md, ...
  etl-api/
    .template.config/template.json
    src/, tests/, .githooks/, .claude/, CLAUDE.md, ...
```

Each template directory is a self-contained `dotnet new` template. `dotnet new install <harness-root>` recursively scans for `.template.config/template.json` and registers both.

## Development

This harness repo doesn't have its own pre-commit hooks (those only fire in scaffolded projects). The CI workflow is the safety net. To verify changes locally before pushing:

```powershell
.\template-tests\scaffold-and-build.ps1 cli
.\template-tests\scaffold-and-build.ps1 etl-api
```

Each invocation installs the chosen template, scaffolds a smoke project into `$env:TEMP`, builds it, runs all four test projects, and uninstalls.
