# dotnet-etl-api-template

A `dotnet new` template for .NET 10 ETL + API projects. Scaffolds:

- 4 source projects: `Core`, `Api`, `Etl`, `Analyzers`
- 4 test projects: `Tests.Unit`, `Tests.Integration`, `Tests.Architecture`, `Tests.Analyzers`
- 12 custom Roslyn analyzers (CI0001–CI0013) enforcing project-agnostic code rules
- 20 architecture tests (NetArchTest + reflection) split across 5 focused fixtures
- Three git/Claude hooks enforcing the development lifecycle
- A `CLAUDE.md` documenting the lifecycle for the scaffolded project

## The pattern

An "ETL + API" solution is the standard shape for a small data platform: one process pulls data in on a schedule (the **ETL**), another exposes it over HTTP (the **API**), and both share the same domain types and persistence layer.

This template scaffolds that as four source projects with strict dependency rules:

| Project | Role | Depends on |
|---------|------|------------|
| **`Core`** | Domain types, interfaces, EF Core context, business logic. Knows nothing about the web or the ingestion job. | (nothing) |
| **`Etl`** | Console app for the periodic ingestion run. Reads from external sources, writes through `Core`. Run on a schedule (cron, Task Scheduler, Hangfire, etc.). | `Core` |
| **`Api`** | ASP.NET Core minimal API. Reads through `Core` and serves it over HTTP. | `Core` |
| **`Analyzers`** | Roslyn analyzers (CI0001–CI0013) that ship with the solution and run on every build. | (nothing) |

The split lets the two workloads scale independently — the ETL might run nightly on a small VM, the API runs continuously behind a load balancer — and keeps each layer testable at its own boundary.

The four test projects mirror the same boundaries:

- **Unit** — fast, in-memory, per-class
- **Integration** — real database, real HTTP, end-to-end
- **Architecture** — enforces the layering rules above, plus DI registration, naming conventions, and one-public-type-per-file
- **Analyzers** — verifies each Roslyn analyzer flags what it should and ignores what it shouldn't

## Install

`dotnet new install` doesn't accept git URLs — clone first, then install from the local `content/` directory:

```powershell
git clone https://github.com/ryan75195/dotnet-etl-api-template
dotnet new install .\dotnet-etl-api-template\content
```

## Use

```powershell
dotnet new etl-api -n MyDataPlatform
cd MyDataPlatform
.\setup.ps1
```

`MyDataPlatform` becomes the project name everywhere — namespaces, project files, the `.slnx`.

After `setup.ps1`:
- git repo is initialized
- `.githooks/` is active
- initial commit is in
- ready for `gh repo create` and the first issue

## Update

```powershell
cd dotnet-etl-api-template
git pull
dotnet new install .\content --force
```

## Uninstall

```powershell
dotnet new uninstall <path-you-used-at-install-time>\content
```

(For example, `dotnet new uninstall .\dotnet-etl-api-template\content`.)

## Repo layout

- `content/` — what gets scaffolded into the user's project
- `content/.template.config/template.json` — template manifest (sourceName, post-actions)
- `template-tests/` — local validation scripts
- `.github/workflows/template-ci.yml` — CI runs scaffold + build + test on every push

## Development

This template repo doesn't have its own pre-commit hooks (those only fire in scaffolded projects). Feature branches + PRs + the template-CI workflow are the safety net.

To verify changes locally before pushing:

```powershell
.\template-tests\scaffold-and-build.ps1
```
