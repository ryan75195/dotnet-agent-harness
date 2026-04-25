# Extract project skeleton into reusable etl-api dotnet template

**Status:** Approved (brainstorming)
**Date:** 2026-04-25
**Related:** Issue #13

## Context

This repo (`global-real-estate`) currently contains:
- 4 source projects (`Core`, `Api`, `Etl`, `Analyzers`) — all empty skeletons or near-empty
- 4 test projects (`Tests.Unit`, `Tests.Integration`, `Tests.Architecture`, `Tests.Analyzers`)
- 12 custom Roslyn analyzers (CI0001–CI0013) enforcing project-agnostic code rules
- 21 architecture tests (NetArchTest + reflection) enforcing layering, naming, DI shape, file structure
- Three git/Claude hooks enforcing the development lifecycle (issue-linked branches, build/format/test before commit, blocked edits on merged branches)
- A `CLAUDE.md` documenting the lifecycle

A file-by-file audit established that ~95% of this is generic scaffolding for any ETL+API .NET project. The only project-specific elements are the literal string `GlobalRealEstate` in namespaces/filenames/the `.slnx`. Domain code does not yet exist.

The intent of this spec is to extract that scaffolding into a `dotnet new` template so future ETL+API projects can start from the same foundation. The original "global real estate" idea will be implemented in a fresh repo scaffolded from the template — i.e. real estate becomes the template's first proof-of-correctness.

## Decisions

### Repo strategy: repurpose-and-relocate (Option 1)

This repo *becomes* the template. After the work is complete:
- This repo is renamed `dotnet-etl-api-template` (`dotnet new etl-api -n MyProject`)
- A new empty `global-real-estate` repo is created
- The first ETL work happens in that new repo, scaffolded from this template

Alternative considered: "keep both purposes in one repo" (Option 2) — rejected because parameterized template files (with substitution tokens) don't compile in-place, only after `dotnet new` substitutes them. Mixing template-source and a working solution in one tree creates conflicts.

### Setup script: single Windows-first `setup.ps1`

Post-scaffold setup (git init, activate `.githooks`, initial commit) runs via a single PowerShell script. Decision drivers:
- The user is on Windows; PowerShell is native
- The hooks themselves are bash, so bash is already a hard runtime requirement regardless of setup-script choice
- Cross-platform `bootstrap.sh` was considered but adds nothing (Windows users with git installed already have git-bash for the hooks)

Tradeoff: a non-Windows user would need PowerShell Core to bootstrap. Acceptable for personal-use template; reconsider if/when the template gets shared.

### Scope: defer Postgres/DbUp/plugin-contract infra

The next chunk of work after the template is complete (Postgres connection, DbUp migration runner, `ICountryEtlJob` plugin contract for the GRE ETL) is *out of scope* for this template extraction. Once that infra exists in the GRE repo and proves out, fold the generic parts back into the template as a v2.

This is consistent with the broader project decision to defer cross-country unification until two countries exist — the principle is "don't template what you haven't validated."

## Architecture

### Final repo layout (`dotnet-etl-api-template`)

```
dotnet-etl-api-template/
  .template.config/
    template.json                ← template manifest
  content/                       ← scaffolded into the user's project
    .claude/
      hooks/block-merged-branch.sh
      settings.json
    .githooks/
      pre-commit
      reference-transaction
    .editorconfig
    .gitignore
    Directory.Build.props
    GlobalRealEstate.slnx        ← name substituted by `dotnet new`
    CLAUDE.md
    README.md                    ← scaffolded project quickstart (new)
    setup.ps1                    ← post-scaffold setup (new)
    src/GlobalRealEstate.Analyzers/
    src/GlobalRealEstate.Api/
    src/GlobalRealEstate.Core/
    src/GlobalRealEstate.Etl/
    tests/.editorconfig
    tests/GlobalRealEstate.Tests.Analyzers/
    tests/GlobalRealEstate.Tests.Architecture/
    tests/GlobalRealEstate.Tests.Integration/
    tests/GlobalRealEstate.Tests.Unit/
  template-tests/
    scaffold-and-build.ps1       ← scaffolds template, builds, runs tests
  .github/workflows/
    template-ci.yml              ← runs template-tests on push/PR
  README.md                      ← template usage docs (new)
```

Why a `content/` subdirectory: lets the template repo have its own README, CI workflow, and template-tests at the root without polluting the scaffolded output. Standard pattern for single-template repos with first-class CI.

### `template.json`

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "ryan75195",
  "classifications": ["ETL", "API", "Console", "Web"],
  "identity": "EtlApi.Template",
  "name": "ETL + API solution",
  "shortName": "etl-api",
  "tags": {
    "language": "C#",
    "type": "solution"
  },
  "sourceName": "GlobalRealEstate",
  "preferNameDirectory": true,
  "primaryOutputs": [
    { "path": "src/GlobalRealEstate.Api/GlobalRealEstate.Api.csproj" },
    { "path": "src/GlobalRealEstate.Etl/GlobalRealEstate.Etl.csproj" }
  ],
  "postActions": [
    {
      "id": "restore",
      "actionId": "210D431B-A78B-4D2F-B762-4ED3E3EA9025",
      "continueOnError": true,
      "manualInstructions": [{ "text": "Run 'dotnet restore'" }]
    }
  ]
}
```

`sourceName` does literal string substitution across filenames, namespaces, and file contents. `dotnet new etl-api -n MyApp` rewrites every occurrence of `GlobalRealEstate` to `MyApp`.

### `setup.ps1` (post-scaffold)

```powershell
$ErrorActionPreference = 'Stop'

Write-Host "Initializing git repo..."
git init -q
git add .

Write-Host "Activating .githooks..."
git config core.hooksPath .githooks

Write-Host "Creating initial commit..."
git commit -q --no-verify -m "Initial scaffold from etl-api template"

Write-Host ""
Write-Host "Done. Next steps:"
Write-Host "  1. dotnet restore; dotnet build"
Write-Host "  2. gh repo create"
Write-Host "  3. gh issue create --title '...'"
```

`--no-verify` on the initial commit is deliberate: the pre-commit hook would run `dotnet build` against an unrestored project. The initial commit captures unmodified template output; every subsequent commit goes through the full hook chain. Documented in scaffolded `README.md`.

### Scaffolded project quickstart

```
dotnet new etl-api -n MyDataPlatform
cd MyDataPlatform
.\setup.ps1
```

### `.githooks/` and `.claude/` activation in scaffolded projects

- `.githooks/` is a directory of bash scripts. Git won't use them until `git config core.hooksPath .githooks` runs — handled by `setup.ps1`.
- `.claude/settings.json` is read automatically by Claude Code when working in the project directory; no activation needed.
- The relative path `bash .claude/hooks/block-merged-branch.sh` in `.claude/settings.json` resolves correctly in the scaffolded project root.
- Executable bit on `.sh` files: not preserved through `dotnet new` on Unix, but not needed on Windows (NTFS doesn't use it). Cross-platform support requires `chmod +x` in `setup.ps1` — out of scope for V1 (Windows-first).

### Template validation (CI)

`.github/workflows/template-ci.yml` runs on every push/PR:
1. `dotnet new install ./` (install template from current source)
2. `dotnet new etl-api -n ScaffoldTest -o /tmp/scaffold-test`
3. `dotnet build` in the scaffolded directory
4. `dotnet test` in the scaffolded directory

This catches regressions when the template's contents change (e.g., a hook breaks, an analyzer rule changes) and the scaffolded project no longer builds.

V1 runs on Windows only (matches Windows-first scope). Add Linux to the matrix later if cross-platform demand emerges.

## Refactor list (M1)

These are cleanups required regardless of templating; the template work just forces the issue.

### Stale references — drop

| File | Change |
|------|--------|
| `tests/.../ArchitectureTests.cs` lines 18–23 | Drop `Core.Search`, `Core.Scraping` from `ServiceNamespaces` array |
| `tests/.../ArchitectureTests.cs` lines 51–61 | Delete `Should_keep_models_free_of_search_and_scraping_dependencies` test (references namespaces that don't exist) |
| `tests/.../ArchitectureTests.cs` lines 272–279 | Drop `Crawler`, `Downloader`, `Chunker`, `Synthesiser` from `AllowedSuffixes` |
| `.githooks/pre-commit` line 99 | Remove `--filter "TestCategory!=RequiresQdrant"` (no Qdrant code in repo) |

### `reference-transaction` fail-open

Add at the top of `.githooks/reference-transaction`:

```bash
# Fail open if there's no GitHub remote yet — local-only repos shouldn't be blocked
git remote get-url origin >/dev/null 2>&1 || exit 0
command -v gh >/dev/null 2>&1 || exit 0
```

Without this, scaffolded projects can't create their first feat branch until they've also done `gh repo create`. The hook still enforces issue-linked branches once a remote exists.

### Smoke tests for empty test projects

Both `Tests.Unit` and `Tests.Integration` currently have only an `AssemblyMarker.cs`. The architecture tests assert `testFixtures.Should().NotBeEmpty()` and `testMethods.Should().NotBeEmpty()`, which will fail on a freshly-scaffolded project.

Add a single trivial fixture to each:

```csharp
namespace GlobalRealEstate.Tests.Unit;

[TestFixture]
public class SmokeTests
{
    [Test]
    public void Should_pass_smoke_check() => Assert.Pass();
}
```

The user replaces these as they write real tests.

## Architecture tests split (M2)

Take the 636-line `ArchitectureTests.cs` (20 tests after M1 deletes one) and split into 5 focused files plus a shared helpers class. All new files go directly under `tests/GlobalRealEstate.Tests.Architecture/` (no subfolder).

### `TestHelpers.cs`

Shared static members:
- `CoreAssembly`, `EtlAssembly`, `ApiAssembly` properties
- `TestAssemblies` array
- `IsRecord(Type)`, `IsDbContext(Type)` helpers
- `FindSolutionRoot()` helper
- `PublicTypePattern`, `TestMethodPattern` regexes

### `LayerDependencyTests.cs` (4 tests)

- `Should_keep_models_free_of_data_layer_dependencies`
- `Should_keep_interfaces_free_of_data_implementation_dependencies`
- `Should_keep_api_free_of_etl_references`
- `Should_keep_etl_free_of_api_dependencies`

### `NamingConventionTests.cs` (8 tests)

- `Should_require_record_types_in_core_models`
- `Should_match_interface_naming_suffix_on_implementations`
- `Should_end_with_entity_for_types_in_entities_namespace`
- `Should_end_with_tests_for_all_test_fixtures`
- `Should_follow_naming_convention_for_all_test_methods`
- `Should_not_use_async_suffix_on_method_names`
- `Should_use_recognised_role_suffix_on_concrete_classes`
- `Should_place_interfaces_in_interfaces_namespace`

### `ServiceShapeTests.cs` (3 tests)

- `Should_require_interfaces_on_core_service_classes`
- `Should_inject_dependencies_as_interfaces_not_concrete_types`
- `Should_not_allow_static_classes_in_commands_namespace`

### `CodeStructureTests.cs` (4 tests)

- `Should_declare_at_most_one_public_type_per_file`
- `Should_not_return_arrays_from_public_methods`
- `Should_have_test_fixture_for_every_public_class`
- `Should_place_test_fixtures_in_namespace_matching_source_assembly`

### `DiRegistrationTests.cs` (1 test)

- `Should_register_every_public_core_interface_in_add_core_services`

Kept solo because it's the load-bearing rule from `CLAUDE.md` (every Core interface must be registered in `AddCoreServices()`).

### Verification

Test count before M2 = 20 (21 minus the one deleted in M1). Test count after M2 = 20, unchanged (mechanical split). Plus 2 smoke tests added in M1 → total = 22. Run `dotnet test` and confirm count matches.

## Milestone plan

Each milestone is a separate issue + feat branch + PR per the existing lifecycle.

### M1 — Cleanup pass

Issue title: `Cleanup: drop stale references and add fail-open guards before templating`

Branch: `feat/<N>-template-cleanup-pass`

Scope: refactor list above. No structural changes.

### M2 — Split architecture tests

Issue title: `Split ArchitectureTests.cs into focused files`

Branch: `feat/<N>-split-architecture-tests`

Scope: architecture tests split above. Mechanical refactor; no behavior change. Test count must match before/after.

### M3 — Template machinery

Issue title: `Restructure repo into dotnet new template layout`

Branch: `feat/<N>-template-machinery`

Scope:
- Move all current content to `content/`
- Add `.template.config/template.json`
- Add `content/setup.ps1`
- Add `content/README.md` (scaffolded project quickstart)
- Add root-level `README.md` (template usage docs)
- Add `template-tests/scaffold-and-build.ps1`
- Add `.github/workflows/template-ci.yml`
- Update `content/CLAUDE.md` to drop GRE-specific sections, keep lifecycle

### M4 — Repo rename + first install

Not a code change; manual GitHub-side ops. Tracked as a checklist on issue #13 (or its own issue if preferred).

Steps:
1. Rename GitHub repo: `global-real-estate` → `dotnet-etl-api-template`
2. Update local git remote URL
3. `dotnet new install <path-to-template-repo>`
4. `dotnet new etl-api -n SmokeTest -o ../smoke-test`
5. Verify scaffold builds + tests pass
6. Delete the throwaway

### M5 — End-to-end validation via fresh `global-real-estate`

Issue title: `Validate template by scaffolding fresh global-real-estate`

Branch: in the *new* repo, not this one.

Steps:
1. Create empty GitHub repo `global-real-estate`
2. `dotnet new etl-api -n GlobalRealEstate` into a new local directory
3. Run `setup.ps1`
4. `gh repo create` and push
5. Create first issue
6. Create first feat branch — verify `reference-transaction` allows it
7. Make a trivial change, commit — verify pre-commit fires (build, format, tests)
8. Open PR, squash-merge
9. Try to keep editing the merged branch — verify `block-merged-branch.sh` trips

After M5 passes, the template repo is "done" until evidence (from GRE work or a second project) suggests something worth folding back.

## Out of scope

- **Postgres / DbUp / `ICountryEtlJob` infra** — generic for any ETL+API but not yet validated. Build in GRE first, fold back later.
- **NuGet packaging** — `dotnet new install <git-url>` is sufficient for personal use.
- **Multi-template support** (`--with-api` / `--without-etl` flags) — single shape for V1; add when a second project requests differently.
- **Cross-platform setup script** — Windows-first; revisit if the template gets shared.
- **Linux CI matrix** — Windows-only V1; add when needed.

## Open questions

None at spec time. Surface during implementation if any emerge.
