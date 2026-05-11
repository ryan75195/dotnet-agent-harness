# EtlApi

ETL + API solution scaffolded from the [dotnet-agent-harness](https://github.com/ryan75195/dotnet-agent-harness) `etl-api` template.

## First-time setup

After scaffolding (`dotnet new etl-api -n EtlApi`), run once:

```powershell
.\setup.ps1
```

This initializes a git repo, activates `.githooks/` for the project lifecycle, and creates the initial commit. Codex hook config is included under `.codex/` for edit-time branch guards.

## Build and test

```powershell
dotnet restore
dotnet build
dotnet test
```

## Development lifecycle

See [CLAUDE.md](./CLAUDE.md) for the full lifecycle (issue → branch → commit → PR).

Quick summary:
1. `gh issue create --title "..."` (every change starts with an issue)
2. `git checkout -b feat/<issue-num>-<slug>` (`reference-transaction` hook verifies the issue exists)
3. Edit + commit (pre-commit hook runs build, format, tests)
4. `gh pr create` and squash-merge

Direct edits and commits to `main` are blocked. Edits to already-merged branches are blocked.
