# `new-project` CLI — design

Date: 2026-07-12
Status: Approved (design), pending implementation plan

## Problem

Scaffolding a project from `agent-project-templates` is currently reachable only
through three Claude Code plugin skills (`new-expo-app`, `new-dotnet-cli`,
`new-dotnet-etl-api`). That means:

- There is no way to scaffold from a plain terminal — the flow only exists inside
  a Claude session, and only if the `agent-harness` plugin is installed.
- The per-type scaffold→stamp→setup→verify logic is duplicated across the three
  skill documents and can drift.

The user's governing principle for this repo: templates and their CLI commands are
standalone and independent from agents; agent helpers (skills, CLAUDE.md, hooks) are
optional conveniences, never a requirement. This feature makes scaffolding a
first-class standalone command, with the skills reduced to thin delegates.

## Goals

- A standalone `new-project` command, runnable from any terminal with no Claude,
  plugin, or agent involved.
- Installed on PATH via a one-time installer, so `new-project ...` works from
  anywhere.
- Flags-only / non-interactive: `new-project <type> <Name> [flags]`; usage on bad
  or missing input.
- One source of truth for the scaffold→stamp→setup→verify pipeline; the three plugin
  skills refactored to delegate to it.
- Extensible: adding a project type is dropping one handler file.

## Non-goals

- No interactive wizard (flags-only was chosen).
- No global `~/.claude/CLAUDE.md` note — the (refactored, already-global) plugin
  skills are the agent path; the CLI is the terminal path.
- No new project types (expo, dotnet-cli, dotnet-etl-api only).
- No config UI, no changes to the underlying per-type scaffolders' behavior.
- Android/cross-platform concerns unchanged.

## Architecture

Everything lives in the repo, self-locating, with zero installed-plugin dependency
at runtime.

1. **`new-project.ps1`** (repo root) — the dispatcher. Finds the repo root via
   `$PSScriptRoot`, so it reaches the scaffolders (`expo/new-app.ps1`, `dotnet/`),
   the provenance stamp (`plugin/scripts/write-stamp.ps1`), and the handlers
   directly. No `resolve-repo` needed by the CLI itself.
2. **`new-project/handlers/*.ps1`** — one self-registering handler per type
   (`expo.ps1`, `dotnet-cli.ps1`, `dotnet-etl-api.ps1`). Each returns a descriptor.
3. **`install.ps1`** (repo root) — installs `new-project` onto PATH.
4. **Refactored plugin skills** — thin delegates that call `new-project`.

### Type handler descriptor

Each handler file evaluates to a hashtable:

```powershell
@{
  Type        = 'expo'
  Description = 'Expo / React Native app (payments, auth, EAS build/deploy)'
  StampName   = 'expo-app'    # value for write-stamp.ps1 -Template
  ExtraArgs   = @(
    @{ Name = 'BundleId'; Required = $false; Help = 'reverse-DNS id, e.g. com.acme.myapp' }
  )
  PreInstall  = $null         # or { param($ctx) dotnet new install "$($ctx.Repo)/dotnet" --force }
  Scaffold    = { param($ctx) & "$($ctx.Repo)/expo/new-app.ps1" -Name $ctx.Name -Destination $ctx.Dest @($ctx.Extra) }
  Verify      = { param($ctx) npm run verify }
}
```

- `Name` and `Destination` are universal, handled by the dispatcher. A handler
  declares only its EXTRA args (expo: `BundleId`; the two dotnet handlers: none).
- **Only-provided extras are passed by splatting.** The dispatcher builds
  `$ctx.Extra` as a hashtable containing only the extra flags the user actually
  supplied, and handlers splat it (`@($ctx.Extra)` / `@ctx.Extra`). This is
  deliberate: passing `-BundleId $null` would override `new-app.ps1`'s own
  `com.example.<name>` default, so an unset optional must be omitted entirely and
  left to fall through to the scaffolder's default. `write-stamp.ps1 -BundleId` is
  likewise passed only when the user supplied it (its default matches
  `new-app.ps1`'s).
- `StampName` maps to `write-stamp.ps1 -Template` (validated set: `cli`, `etl-api`,
  `expo-app`).
- `$ctx` passed to the scriptblocks carries: `Repo`, `Name`, `Dest`, and any extra
  args (e.g. `BundleId`).

The three descriptors:

| Type | StampName | ExtraArgs | PreInstall | Scaffold | Verify |
|---|---|---|---|---|---|
| `expo` | `expo-app` | `BundleId?` | none | `expo/new-app.ps1 -Name -BundleId -Destination` | `npm run verify` |
| `dotnet-cli` | `cli` | none | `dotnet new install "<repo>/dotnet" --force` | `dotnet new cli -n <Name> -o <Dest>` | `dotnet build --no-incremental` then `dotnet test --no-build --verbosity minimal` |
| `dotnet-etl-api` | `etl-api` | none | `dotnet new install "<repo>/dotnet" --force` | `dotnet new etl-api -n <Name> -o <Dest>` | `dotnet build --no-incremental` then `dotnet test --no-build --verbosity minimal` |

### Dispatcher flow

`new-project <type> <Name> [flags]`:

1. **Discover** handlers by enumerating `new-project/handlers/*.ps1` and evaluating
   each to its descriptor. Adding a type = dropping one file; nothing central to edit.
2. **Usage** on no args, `-h`/`--help`, or unknown type: list each type, its
   description, and accepted flags; exit 0 for help, non-zero for an unknown type.
3. **Validate**: `Name` present and PascalCase (`^[A-Z][A-Za-z0-9]*$`); only the
   type's declared extra flags (plus universal `-Destination`) allowed — an
   undeclared flag (e.g. `-BundleId` on `dotnet-cli`) is an error; required extras
   present.
4. **Build `$ctx`**: `Repo = $PSScriptRoot`, `Name`, `Dest` (default `<cwd>/<Name>`),
   plus extras.
5. **Pipeline** (once, in the dispatcher):
   `PreInstall?` → `Scaffold` (handler) →
   `write-stamp.ps1 -ProjectDir $Dest -Template <StampName> -ProjectName $Name -RepoPath $Repo [-BundleId ...]` →
   `cd $Dest; ./setup.ps1` →
   if `.harness.json` is untracked after setup, `git add .harness.json; git commit --no-verify -m "Add harness provenance stamp"` →
   `Verify` (handler) → report (path, active guardrails, next steps).
6. Any step fails → clear message, non-zero exit.

`-DryRun` switch: after validation, print the resolved plan (type, destination,
ordered step list) and exit 0 without side effects. Backbone of the tests and useful
to users.

### Why scriptblock handlers, not a JSON registry

The two families scaffold very differently (expo runs a PowerShell script; dotnet
does `dotnet new install` then `dotnet new <template>`). Real PowerShell scriptblocks
keep that logic honest and unit-testable; templated shell strings in JSON would be
brittle. Folder-discovery makes each type self-registering.

## Installer (`install.ps1`)

- Creates a bin dir (default `~/.agent-harness/bin`, the dir `resolve-repo` already
  uses) containing two shims, both invoking
  `powershell -NoProfile -ExecutionPolicy Bypass -File "<repo>\new-project.ps1" <args>`
  with `<repo>` pinned at install time (`$PSScriptRoot` of `install.ps1`):
  - `new-project.cmd` (cmd / PowerShell), `%*` for args
  - `new-project` (POSIX sh shim for Git Bash), `"$@"` for args
- Adds the bin dir to the **User PATH** idempotently (only if absent). Effective in
  new terminals.
- Writes `~/.agent-harness/config.json` `repoPath` → this checkout **only if the file
  or key is unset** (never clobbers), so the plugin skills' `resolve-repo` and the CLI
  resolve to the same repo.
- Params: `-BinDir <dir>` (override target), `-NoPath` (skip PATH edit) — for control
  and hermetic testing.
- Reports the bin dir, the "open a new terminal" note, and uninstall instructions
  (remove the dir from PATH, delete the shims).

## Skill refactor

Each of the three skills collapses to a thin delegate:

1. `resolve-repo.ps1` → `$repo`.
2. Ask for `Name` (and `BundleId` for expo if the user mentions shipping) only if
   missing.
3. `& "$repo/new-project.ps1" <type> <Name> [flags]`.
4. Relay the CLI's output/errors (git-identity failure, verify failure) and report
   the CLI's result.

Skills keep their distinct trigger descriptions ("new mobile app", "new api",
"new cli tool") so discovery/triggering is preserved. They no longer duplicate
scaffold/stamp/setup/verify — the CLI owns it. `write-stamp.ps1` stays in
`plugin/scripts/` and is called by the CLI via a repo-relative path (a repo file
present for any cloner, not an installed-plugin dependency).

## Testing

- **Dispatcher unit tests** — plain-PowerShell, in the repo's existing throw-on-failure
  `template-tests` style, no scaffolding:
  - handler discovery finds `expo`, `dotnet-cli`, `dotnet-etl-api`
  - bare / `-h` usage lists all three with their flags
  - unknown type → error, non-zero exit
  - missing `Name` → error; non-PascalCase `Name` → error
  - an undeclared flag for a type (e.g. `-BundleId` on `dotnet-cli`) → error
  - `-DryRun` prints the correct plan per type
- **Installer test** — `install.ps1 -BinDir <temp> -NoPath` creates both shims,
  each referencing the repo's `new-project.ps1`, without touching real PATH.
- **One end-to-end** — `new-project dotnet-cli <Name>` into a temp dir → asserts the
  project exists, `.harness.json` is stamped, and it builds. (Cheapest real path; the
  existing per-type smoke tests cover the scaffolders themselves in depth.)

## Success criteria

- After `install.ps1` and opening a new terminal, `new-project` (no args) prints usage
  listing the three types; `new-project expo MyApp -BundleId com.acme.myapp` scaffolds,
  stamps, sets up, and verifies a working Expo project with no agent involved.
- The three plugin skills produce the same result by delegating to the CLI (one code
  path).
- Adding a future type requires only a new handler file.
