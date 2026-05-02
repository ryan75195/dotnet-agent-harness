# dotnet-console-template — design

Date: 2026-05-02

## Goal

A `dotnet new` template for .NET 10 console / CLI projects that mirrors the
guardrails of `dotnet-etl-api-template` (custom analyzers, architecture tests,
git/Claude hooks, CI) but ships a single console entry point instead of two
(Api + Etl). The template is the "small one" — when a project needs a second
entry point, the existing etl-api template is the right choice.

## Repo metadata

- Repo folder: `C:\Users\ryan7\programming\dotnet-console-template\`
- Sibling of the existing `dotnet-etl-api-template`
- `template.json`:
  - `identity`: `Cli.Template`
  - `name`: `Console CLI solution`
  - `shortName`: `cli` (avoids clash with Microsoft's built-in `console` template)
  - `sourceName`: `ConsoleApp`
  - `classifications`: `["Console", "CLI"]`
  - `tags`: `{ "language": "C#", "type": "solution" }`
  - `primaryOutputs`: single entry pointing at `src/ConsoleApp.Cli/ConsoleApp.Cli.csproj`
  - `postActions`: `restore` (unchanged from etl-api)

Scaffolded usage:

```powershell
dotnet new cli -n MyTool
cd MyTool
.\setup.ps1
```

## Approach

Copy the etl-api template's repo wholesale, then transform. Starting from the
existing template is faster, lower-risk, and keeps the analyzer/hook code
byte-identical so future changes to either template port cleanly.

The transformation is mechanical for ~95% of files (string replace
`GlobalRealEstate` → `ConsoleApp`, `etl-api` → `cli`, drop two projects from
the solution and template manifest). Only one file has semantic changes:
`tests/.../LayerDependencyTests.cs`.

## Project shape (`content/`)

### `src/` — three projects

| Project | SDK | Purpose | Key contents |
|---|---|---|---|
| `ConsoleApp.Core` | `Microsoft.NET.Sdk` | Domain + DI extensions | `AssemblyMarker.cs`, `ServiceCollectionExtensions.cs` (with `AddCoreServices()` empty stub) — copied from etl-api Core verbatim, namespace renamed |
| `ConsoleApp.Cli` | `Microsoft.NET.Sdk.Worker` | The console entry point | `AssemblyMarker.cs`, `Program.cs` containing exactly `var builder = Host.CreateApplicationBuilder(args); await builder.Build().RunAsync();` — same bones as the existing `Etl` project |
| `ConsoleApp.Analyzers` | `Microsoft.NET.Sdk` (netstandard2.0) | The 13 custom Roslyn analyzers (CI0001–CI0013) | Copied verbatim, namespace renamed only |

`ConsoleApp.Cli.csproj` is a clone of `GlobalRealEstate.Etl.csproj`:
- Same Worker SDK
- Same `Microsoft.Extensions.Hosting` + `Serilog.*` package references
- A new hardcoded `UserSecretsId` GUID (the etl-api template hardcodes one for `Etl`; we generate one fresh value so the two templates don't collide if both are installed)
- Same `InternalsVisibleTo ConsoleApp.Tests.Unit`
- Same `ProjectReference` to `ConsoleApp.Core`

The `Etl` and `Api` projects from the source template are **dropped entirely**.

### `tests/` — four projects

All four test projects from etl-api are carried over:

- `ConsoleApp.Tests.Unit`
- `ConsoleApp.Tests.Integration` (kept despite single entry point — option A from brainstorming; preserves parity and leaves room for "spawn-the-CLI" integration tests later)
- `ConsoleApp.Tests.Architecture`
- `ConsoleApp.Tests.Analyzers`

Test project `.csproj` files copy across with `GlobalRealEstate` → `ConsoleApp`
substitutions only. `tests/.editorconfig` copies unchanged.

## Architecture-test changes

`tests/ConsoleApp.Tests.Architecture/LayerDependencyTests.cs` — only file
needing semantic edits.

**Keep:**
- `Should_keep_models_free_of_data_layer_dependencies`
- `Should_keep_interfaces_free_of_data_implementation_dependencies`

**Drop:**
- `Should_keep_api_free_of_etl_references`
- `Should_keep_etl_free_of_api_dependencies`

**Add:**

```csharp
[Test]
public void Should_keep_cli_depending_only_on_first_party_core()
{
    var cliAssembly = Assembly.Load("ConsoleApp.Cli");
    var firstPartyRefs = cliAssembly.GetReferencedAssemblies()
        .Select(a => a.Name)
        .Where(name => name?.StartsWith("ConsoleApp", StringComparison.Ordinal) == true)
        .ToList();

    firstPartyRefs.Should().BeEquivalentTo(new[] { "ConsoleApp.Core" },
        "Cli is the sole entry point and may only reference Core within first-party assemblies");
}
```

`tests/ConsoleApp.Tests.Architecture/TestHelpers.cs`:
- Replace `EtlAssembly` and `ApiAssembly` properties with a single
  `CliAssembly => typeof(Cli.AssemblyMarker).Assembly`
- Keep `IsRecord` and `IsDbContext` helpers and the `Microsoft.EntityFrameworkCore`
  using (project-agnostic; no use yet but parity-preserving for future ETL-style
  console apps)
- `TestAssemblies` array drops the second entry-point assembly (no Api), keeps
  Architecture / Unit / Integration / Analyzers

The other four arch fixtures (`NamingConventionTests`, `ServiceShapeTests`,
`CodeStructureTests`, `DiRegistrationTests`) iterate over `Core` /
`TestAssemblies` and don't reference Api/Etl by name. They port unchanged
aside from the namespace rename.

## Analyzers

All 13 analyzer source files (plus `AnalyzerConstants.cs`) in `src/ConsoleApp.Analyzers/` and their
corresponding test files in `tests/ConsoleApp.Tests.Analyzers/` copy
verbatim. The only diff is the namespace declaration line at the top of each
file: `GlobalRealEstate.Analyzers` → `ConsoleApp.Analyzers`.

`Directory.Build.props` clones with the project-name guard updated:
`'$(MSBuildProjectName)' != 'GlobalRealEstate.Analyzers'` →
`'$(MSBuildProjectName)' != 'ConsoleApp.Analyzers'` (two occurrences).

## Hooks, CI, and scripts

| File | Change |
|---|---|
| `content/.githooks/pre-commit` | Verbatim |
| `content/.githooks/reference-transaction` | Verbatim |
| `content/.claude/hooks/block-merged-branch.sh` | Verbatim |
| `content/.claude/settings.json` | Verbatim |
| `content/.editorconfig` | Verbatim |
| `content/.gitignore` | Verbatim |
| `content/setup.ps1` | Commit message: `"Initial scaffold from etl-api template"` → `"Initial scaffold from cli template"` |
| `content/Directory.Build.props` | Project-name guard updated as above |
| `content/CLAUDE.md` | Architecture section: project list rewritten to `Core / Cli / Analyzers` (3 src) + 4 test projects; "API and ETL are independent entry points" framing dropped |
| `.github/workflows/template-ci.yml` | `dotnet new etl-api -n …` → `dotnet new cli -n …`; assertion updated to look for `ConsoleApp.Cli` rather than Api+Etl |
| `template-tests/scaffold-and-build.ps1` | Same as workflow |
| `README.md` (repo root) | Rewritten: project list, install/use snippets, reflect 3 src + 4 test, CI mentions `cli` not `etl-api` |
| `content/README.md` | Verbatim — describes the scaffolded project's bootstrap, generic enough that it doesn't need changes |
| `content/GlobalRealEstate.slnx` → `content/ConsoleApp.slnx` | Drops `Api` and `Etl` project entries; `src/` folder lists `Analyzers`, `Cli`, `Core` only |

## CLAUDE.md changes (in `content/`)

Two specific edits to the existing CLAUDE.md text:

1. **Architecture bullet (line 31):** rewrite to reflect three src projects
   (`Core`, `Cli`, `Analyzers`) and four test projects, and remove the
   "independent entry points" framing.
2. **All project-name references** in the file: `GlobalRealEstate` →
   `ConsoleApp`, `Api`/`Etl` collapsed to `Cli` where mentioned.

Lifecycle and Code style sections are unchanged.

## What ships in `Program.cs`

Bare host bootstrap, identical to the current `Etl/Program.cs`:

```csharp
var builder = Host.CreateApplicationBuilder(args);
await builder.Build().RunAsync();
```

No CLI argument parser preinstalled. Brainstorming Q2 chose option A (bare):
the template's value is the guardrails, not pre-chosen libraries.

## Out of scope

- No `System.CommandLine` / `Spectre.Console.Cli` / other CLI framework
- No `appsettings.json` or `Properties/launchSettings.json` beyond what
  `Sdk.Worker` provides by default
- No removal of EFCore from `TestHelpers.cs` despite no immediate use —
  parity-preserving so updates port between templates
- No multi-project sample (worker + cli, or sub-commands as separate projects) —
  if that's needed, it's a different template

## Validation

After scaffolding finishes:

1. `template-tests/scaffold-and-build.ps1` runs `dotnet new cli -n SampleApp`
   in a temp directory, then `dotnet restore && dotnet build && dotnet test`,
   asserting all three succeed.
2. The CI workflow (`.github/workflows/template-ci.yml`) runs the same on every
   push.
3. Manual install + scaffold once locally before declaring done:
   `dotnet new install .\dotnet-console-template\content` then
   `dotnet new cli -n Smoke && cd Smoke && dotnet build && dotnet test`.

## Implementation outline (handed to writing-plans next)

1. Initialize repo skeleton (already done as part of spec creation).
2. Bulk-copy `dotnet-etl-api-template` → `dotnet-console-template`, excluding
   `.git/`, `.vs/`, `bin/`, `obj/`, `docs/superpowers/`.
3. Delete `content/src/GlobalRealEstate.Api/` and `content/src/GlobalRealEstate.Etl/`.
4. Rename all directories `GlobalRealEstate.*` → `ConsoleApp.*` (and the
   `.slnx` file).
5. Bulk string-replace `GlobalRealEstate` → `ConsoleApp` across all retained
   files. Drop the `Cli` project files into place (clone of the deleted Etl
   project).
6. Edit `LayerDependencyTests.cs` and `TestHelpers.cs` as specified.
7. Edit `template.json` (drop Api primaryOutput, change identity / shortName /
   sourceName / classifications / name).
8. Edit `ConsoleApp.slnx` (drop two projects).
9. Edit `setup.ps1` commit message, top-level `README.md`, `CLAUDE.md`
   architecture line, CI workflow, template-tests script.
10. Run `template-tests/scaffold-and-build.ps1` locally; fix anything broken.
11. Initial commit on the new repo.
