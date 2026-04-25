# dotnet-etl-api-template

A `dotnet new` template for .NET 10 ETL + API projects. Scaffolds:

- 4 source projects: `Core`, `Api`, `Etl`, `Analyzers`
- 4 test projects: `Tests.Unit`, `Tests.Integration`, `Tests.Architecture`, `Tests.Analyzers`
- 12 custom Roslyn analyzers (CI0001–CI0013) enforcing project-agnostic code rules
- 20 architecture tests (NetArchTest + reflection) split across 5 focused fixtures
- Three git/Claude hooks enforcing the development lifecycle
- A `CLAUDE.md` documenting the lifecycle for the scaffolded project

## Install

```powershell
dotnet new install https://github.com/ryan75195/dotnet-etl-api-template
```

(Or from a local clone: `dotnet new install <path-to-repo>`.)

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
dotnet new install https://github.com/ryan75195/dotnet-etl-api-template
```

(Same command; pulls the latest from `main`.)

## Uninstall

```powershell
dotnet new uninstall https://github.com/ryan75195/dotnet-etl-api-template
```

(Or whatever path you used at install time.)

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
