# dotnet-console-template Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a `dotnet new` template at `C:\Users\ryan7\programming\dotnet-console-template\` that mirrors the guardrails of `dotnet-etl-api-template` but ships a single console entry point (`Cli`) instead of two (`Api` + `Etl`).

**Architecture:** Copy the etl-api template's `content/`, top-level files, and CI workflow wholesale into the already-initialised dotnet-console-template repo, drop the `Api`/`Etl` projects, add a single `Cli` project (clone of the dropped `Etl`), bulk-rename `GlobalRealEstate` → `ConsoleApp`, then surgically edit a small set of files (slnx, template.json, LayerDependencyTests, TestHelpers, CLAUDE.md, two README files, CI workflow, scaffold script, setup.ps1).

**Tech Stack:** .NET 10, `Microsoft.NET.Sdk.Worker` (Cli), `Microsoft.NET.Sdk` (Core, Analyzers), NUnit + FluentAssertions + NetArchTest, custom Roslyn analyzers, PowerShell scripts, GitHub Actions, bash for hooks.

**Pre-state:** the repo is already initialised (`git init -b main`) at `C:\Users\ryan7\programming\dotnet-console-template\` with a single commit containing `docs/superpowers/specs/2026-05-02-dotnet-console-template-design.md` and this plan file.

---

## Constants used throughout

- **Source repo:** `C:\Users\ryan7\programming\dotnet-etl-api-template`
- **Target repo:** `C:\Users\ryan7\programming\dotnet-console-template`
- **Old name:** `GlobalRealEstate` (case-sensitive substring used in csproj, namespaces, slnx, etc.)
- **New name:** `ConsoleApp`
- **Old short name:** `etl-api`
- **New short name:** `cli`
- **New UserSecretsId GUID for Cli project:** `7c4a9b8e-3d12-4f5a-92e1-0b8c4d3e9a76` (a freshly generated value, distinct from the etl-api Etl project's `d34a1e27-6b5f-4a0c-9d81-2f8c6d9e4a11`)

---

## File structure

After the plan completes, `dotnet-console-template/` contains:

```
.github/workflows/template-ci.yml
.gitignore
README.md
docs/superpowers/specs/2026-05-02-dotnet-console-template-design.md   (already there)
docs/superpowers/plans/2026-05-02-dotnet-console-template.md          (already there)
template-tests/scaffold-and-build.ps1
content/
  .claude/hooks/block-merged-branch.sh
  .claude/settings.json
  .editorconfig
  .githooks/pre-commit
  .githooks/reference-transaction
  .gitignore
  .template.config/template.json
  CLAUDE.md
  ConsoleApp.slnx
  Directory.Build.props
  README.md
  setup.ps1
  src/
    ConsoleApp.Analyzers/   (13 analyzer .cs files + AnalyzerConstants.cs + .csproj)
    ConsoleApp.Cli/         (Program.cs, AssemblyMarker.cs, .csproj)
    ConsoleApp.Core/        (ServiceCollectionExtensions.cs, AssemblyMarker.cs, .csproj)
  tests/
    .editorconfig
    ConsoleApp.Tests.Analyzers/        (13 *AnalyzerTests.cs + .csproj)
    ConsoleApp.Tests.Architecture/     (5 *Tests.cs + TestHelpers.cs + .csproj)
    ConsoleApp.Tests.Integration/      (SmokeTests.cs, AssemblyMarker.cs, .csproj)
    ConsoleApp.Tests.Unit/             (SmokeTests.cs, AssemblyMarker.cs, .csproj)
```

Files semantically edited (vs verbatim copies):
- `content/ConsoleApp.slnx` (drop two project entries)
- `content/.template.config/template.json` (identity, name, shortName, primaryOutputs, classifications)
- `content/Directory.Build.props` (project-name guard — handled by bulk rename)
- `content/setup.ps1` (commit message)
- `content/CLAUDE.md` (architecture section)
- `content/README.md` (description text)
- `content/src/ConsoleApp.Cli/*` (newly created from a clone of the dropped Etl project)
- `content/tests/ConsoleApp.Tests.Architecture/LayerDependencyTests.cs` (drop 2 tests, add 1)
- `content/tests/ConsoleApp.Tests.Architecture/TestHelpers.cs` (Etl/Api → Cli)
- `template-tests/scaffold-and-build.ps1`
- `.github/workflows/template-ci.yml`
- `README.md` (top-level)

---

## Task 1: Bulk-copy the etl-api template into the new repo

**Files:**
- Copy from: `C:\Users\ryan7\programming\dotnet-etl-api-template\`
- Copy to: `C:\Users\ryan7\programming\dotnet-console-template\`
- Excluded: `.git/`, `.vs/`, `bin/`, `obj/`, `docs/superpowers/` (the new repo already has its own)

- [ ] **Step 1: Copy everything except excluded directories**

Run from any cwd (the command uses absolute paths):

```bash
cd "/c/Users/ryan7/programming/dotnet-etl-api-template" && \
  rsync -a --exclude='.git/' --exclude='.vs/' --exclude='bin/' --exclude='obj/' --exclude='docs/superpowers/' \
    ./ "/c/Users/ryan7/programming/dotnet-console-template/"
```

If `rsync` is unavailable, use:

```bash
cd "/c/Users/ryan7/programming/dotnet-etl-api-template" && \
  find . -type d \( -name '.git' -o -name '.vs' -o -name 'bin' -o -name 'obj' \) -prune -o -type f -print | \
  grep -v '^./docs/superpowers/' | \
  while read f; do mkdir -p "/c/Users/ryan7/programming/dotnet-console-template/$(dirname "$f")"; cp "$f" "/c/Users/ryan7/programming/dotnet-console-template/$f"; done
```

- [ ] **Step 2: Verify the copy**

```bash
ls "/c/Users/ryan7/programming/dotnet-console-template/content/src/"
```

Expected output (still using the OLD names, the renames happen later):

```
GlobalRealEstate.Analyzers
GlobalRealEstate.Api
GlobalRealEstate.Core
GlobalRealEstate.Etl
```

```bash
ls "/c/Users/ryan7/programming/dotnet-console-template/docs/superpowers/specs/"
```

Expected: `2026-05-02-dotnet-console-template-design.md` (proof the spec wasn't overwritten)

- [ ] **Step 3: Stage but do not commit yet**

```bash
cd "/c/Users/ryan7/programming/dotnet-console-template" && git add -A && git status --short | head -20
```

Expected: lots of `A  content/...` lines (files staged for first commit on this content).

---

## Task 2: Drop the Api and Etl projects

**Files:**
- Delete: `content/src/GlobalRealEstate.Api/` (entire directory)
- Delete: `content/src/GlobalRealEstate.Etl/` (entire directory)

The two entry-point projects from the etl-api template are not part of the console template.

- [ ] **Step 1: Delete both directories**

```bash
cd "/c/Users/ryan7/programming/dotnet-console-template" && \
  rm -rf content/src/GlobalRealEstate.Api content/src/GlobalRealEstate.Etl
```

- [ ] **Step 2: Verify**

```bash
ls "/c/Users/ryan7/programming/dotnet-console-template/content/src/"
```

Expected:

```
GlobalRealEstate.Analyzers
GlobalRealEstate.Core
```

- [ ] **Step 3: Re-stage**

```bash
cd "/c/Users/ryan7/programming/dotnet-console-template" && git add -A
```

---

## Task 3: Rename `GlobalRealEstate.*` directories to `ConsoleApp.*`

**Files:**
- Rename: `content/src/GlobalRealEstate.Analyzers/` → `content/src/ConsoleApp.Analyzers/`
- Rename: `content/src/GlobalRealEstate.Core/` → `content/src/ConsoleApp.Core/`
- Rename: `content/tests/GlobalRealEstate.Tests.Analyzers/` → `content/tests/ConsoleApp.Tests.Analyzers/`
- Rename: `content/tests/GlobalRealEstate.Tests.Architecture/` → `content/tests/ConsoleApp.Tests.Architecture/`
- Rename: `content/tests/GlobalRealEstate.Tests.Integration/` → `content/tests/ConsoleApp.Tests.Integration/`
- Rename: `content/tests/GlobalRealEstate.Tests.Unit/` → `content/tests/ConsoleApp.Tests.Unit/`

The `.csproj` files inside each directory have the matching prefix and get renamed too.

- [ ] **Step 1: Rename directories**

```bash
cd "/c/Users/ryan7/programming/dotnet-console-template/content/src" && \
  mv GlobalRealEstate.Analyzers ConsoleApp.Analyzers && \
  mv GlobalRealEstate.Core ConsoleApp.Core
cd "/c/Users/ryan7/programming/dotnet-console-template/content/tests" && \
  mv GlobalRealEstate.Tests.Analyzers ConsoleApp.Tests.Analyzers && \
  mv GlobalRealEstate.Tests.Architecture ConsoleApp.Tests.Architecture && \
  mv GlobalRealEstate.Tests.Integration ConsoleApp.Tests.Integration && \
  mv GlobalRealEstate.Tests.Unit ConsoleApp.Tests.Unit
```

- [ ] **Step 2: Rename `.csproj` files inside each renamed directory**

```bash
cd "/c/Users/ryan7/programming/dotnet-console-template" && \
  mv content/src/ConsoleApp.Analyzers/GlobalRealEstate.Analyzers.csproj \
     content/src/ConsoleApp.Analyzers/ConsoleApp.Analyzers.csproj && \
  mv content/src/ConsoleApp.Core/GlobalRealEstate.Core.csproj \
     content/src/ConsoleApp.Core/ConsoleApp.Core.csproj && \
  mv content/tests/ConsoleApp.Tests.Analyzers/GlobalRealEstate.Tests.Analyzers.csproj \
     content/tests/ConsoleApp.Tests.Analyzers/ConsoleApp.Tests.Analyzers.csproj && \
  mv content/tests/ConsoleApp.Tests.Architecture/GlobalRealEstate.Tests.Architecture.csproj \
     content/tests/ConsoleApp.Tests.Architecture/ConsoleApp.Tests.Architecture.csproj && \
  mv content/tests/ConsoleApp.Tests.Integration/GlobalRealEstate.Tests.Integration.csproj \
     content/tests/ConsoleApp.Tests.Integration/ConsoleApp.Tests.Integration.csproj && \
  mv content/tests/ConsoleApp.Tests.Unit/GlobalRealEstate.Tests.Unit.csproj \
     content/tests/ConsoleApp.Tests.Unit/ConsoleApp.Tests.Unit.csproj
```

- [ ] **Step 3: Rename the solution file**

```bash
cd "/c/Users/ryan7/programming/dotnet-console-template/content" && \
  mv GlobalRealEstate.slnx ConsoleApp.slnx
```

- [ ] **Step 4: Verify no `GlobalRealEstate.*` filenames remain**

```bash
cd "/c/Users/ryan7/programming/dotnet-console-template" && \
  find . -name 'GlobalRealEstate*' -not -path './.git/*'
```

Expected: empty output.

---

## Task 4: Bulk text-replace `GlobalRealEstate` → `ConsoleApp`

**Files:** every text file under the repo except `docs/superpowers/` (those describe the work and reference the old name on purpose) and `.git/`.

This pass updates: namespace declarations, `using` directives, `csproj` `<ProjectReference>` paths, `slnx` `<Project Path>` entries, `template.json`, `Directory.Build.props` project-name guards, hook scripts that mention assemblies, etc.

- [ ] **Step 1: Run the replacement**

```bash
cd "/c/Users/ryan7/programming/dotnet-console-template" && \
  find . -type f \
    \( -name '*.cs' -o -name '*.csproj' -o -name '*.props' -o -name '*.json' \
       -o -name '*.md' -o -name '*.yml' -o -name '*.yaml' -o -name '*.ps1' \
       -o -name '*.sh' -o -name '*.slnx' -o -name '*.editorconfig' \
       -o -name '.editorconfig' -o -name '.gitignore' \
       -o -name 'pre-commit' -o -name 'reference-transaction' \) \
    -not -path './.git/*' \
    -not -path './docs/superpowers/*' \
    -exec sed -i 's/GlobalRealEstate/ConsoleApp/g' {} +
```

- [ ] **Step 2: Verify no `GlobalRealEstate` strings remain in non-doc files**

```bash
cd "/c/Users/ryan7/programming/dotnet-console-template" && \
  grep -r "GlobalRealEstate" . \
    --exclude-dir=.git \
    --exclude-dir=docs \
    --exclude-dir=bin \
    --exclude-dir=obj
```

Expected: empty output.

- [ ] **Step 3: Spot-check the slnx and a csproj**

```bash
cat "/c/Users/ryan7/programming/dotnet-console-template/content/ConsoleApp.slnx"
```

Expected (still showing all 4 src entries — Api/Etl removal happens in Task 7):

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/ConsoleApp.Analyzers/ConsoleApp.Analyzers.csproj" />
    <Project Path="src/ConsoleApp.Api/ConsoleApp.Api.csproj" />
    <Project Path="src/ConsoleApp.Core/ConsoleApp.Core.csproj" />
    <Project Path="src/ConsoleApp.Etl/ConsoleApp.Etl.csproj" />
  </Folder>
  ...
```

(The Api and Etl entries are now bogus — those directories were deleted in Task 2. Task 7 fixes the slnx.)

```bash
cat "/c/Users/ryan7/programming/dotnet-console-template/content/src/ConsoleApp.Core/ConsoleApp.Core.csproj"
```

Expected: a normal csproj with no `GlobalRealEstate` strings remaining.

---

## Task 5: Replace `etl-api` short name and project-name strings outside of `content/`

**Files:**
- Modify: `README.md` (top-level)
- Modify: `template-tests/scaffold-and-build.ps1`
- Modify: `.github/workflows/template-ci.yml`
- Modify: `content/setup.ps1` (commit message)
- Modify: `content/README.md` (link, name)

The `etl-api` short name appears in README install/use snippets, the CI workflow's `dotnet new etl-api` invocation, and the smoke-test script's project name and `dotnet new etl-api` call. Replace with `cli`.

- [ ] **Step 1: Bulk-replace `etl-api` → `cli`**

```bash
cd "/c/Users/ryan7/programming/dotnet-console-template" && \
  find . -type f \
    \( -name '*.md' -o -name '*.yml' -o -name '*.yaml' -o -name '*.ps1' \
       -o -name '*.json' -o -name '*.sh' \) \
    -not -path './.git/*' \
    -not -path './docs/superpowers/*' \
    -exec sed -i 's/etl-api/cli/g' {} +
```

- [ ] **Step 2: Verify**

```bash
cd "/c/Users/ryan7/programming/dotnet-console-template" && \
  grep -r "etl-api" . \
    --exclude-dir=.git \
    --exclude-dir=docs \
    --exclude-dir=bin \
    --exclude-dir=obj
```

Expected: empty output.

- [ ] **Step 3: Spot-check the smoke-test script**

```bash
cat "/c/Users/ryan7/programming/dotnet-console-template/template-tests/scaffold-and-build.ps1"
```

Expected: `cli-template-smoke` and `dotnet new cli` in the script body, no `etl-api` strings.

---

## Task 6: Create the `ConsoleApp.Cli` project

**Files:**
- Create: `content/src/ConsoleApp.Cli/ConsoleApp.Cli.csproj`
- Create: `content/src/ConsoleApp.Cli/Program.cs`
- Create: `content/src/ConsoleApp.Cli/AssemblyMarker.cs`

The `Cli` project is a clone of the dropped `Etl` project's bones, with a fresh `UserSecretsId`.

- [ ] **Step 1: Create the project directory**

```bash
mkdir -p "/c/Users/ryan7/programming/dotnet-console-template/content/src/ConsoleApp.Cli"
```

- [ ] **Step 2: Write `ConsoleApp.Cli.csproj`**

Path: `content/src/ConsoleApp.Cli/ConsoleApp.Cli.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>dotnet-ConsoleApp.Cli-7c4a9b8e-3d12-4f5a-92e1-0b8c4d3e9a76</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.3" />
    <PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Serilog.Settings.Configuration" Version="8.0.4" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\ConsoleApp.Core\ConsoleApp.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>ConsoleApp.Tests.Unit</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Write `Program.cs`**

Path: `content/src/ConsoleApp.Cli/Program.cs`

```csharp
var builder = Host.CreateApplicationBuilder(args);
await builder.Build().RunAsync();
```

- [ ] **Step 4: Write `AssemblyMarker.cs`**

Path: `content/src/ConsoleApp.Cli/AssemblyMarker.cs`

```csharp
namespace ConsoleApp.Cli;

public static class AssemblyMarker;
```

- [ ] **Step 5: Verify**

```bash
ls "/c/Users/ryan7/programming/dotnet-console-template/content/src/ConsoleApp.Cli"
```

Expected:

```
AssemblyMarker.cs
ConsoleApp.Cli.csproj
Program.cs
```

---

## Task 7: Edit `ConsoleApp.slnx` — drop Api and Etl entries

**Files:**
- Modify: `content/ConsoleApp.slnx`

Remove the Api and Etl `<Project>` lines, add the new `Cli` line. Folder ordering keeps alphabetical.

- [ ] **Step 1: Overwrite the file**

Path: `content/ConsoleApp.slnx`

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/ConsoleApp.Analyzers/ConsoleApp.Analyzers.csproj" />
    <Project Path="src/ConsoleApp.Cli/ConsoleApp.Cli.csproj" />
    <Project Path="src/ConsoleApp.Core/ConsoleApp.Core.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/ConsoleApp.Tests.Analyzers/ConsoleApp.Tests.Analyzers.csproj" />
    <Project Path="tests/ConsoleApp.Tests.Architecture/ConsoleApp.Tests.Architecture.csproj" />
    <Project Path="tests/ConsoleApp.Tests.Integration/ConsoleApp.Tests.Integration.csproj" />
    <Project Path="tests/ConsoleApp.Tests.Unit/ConsoleApp.Tests.Unit.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 2: Verify**

```bash
cat "/c/Users/ryan7/programming/dotnet-console-template/content/ConsoleApp.slnx"
```

Expected: 3 src entries (Analyzers, Cli, Core), 4 tests entries, no Api or Etl.

---

## Task 8: Edit `template.json`

**Files:**
- Modify: `content/.template.config/template.json`

Adjust identity, name, shortName, sourceName, classifications, primaryOutputs.

- [ ] **Step 1: Overwrite the file**

Path: `content/.template.config/template.json`

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "ryan75195",
  "classifications": ["Console", "CLI"],
  "identity": "Cli.Template",
  "name": "Console CLI solution",
  "shortName": "cli",
  "tags": {
    "language": "C#",
    "type": "solution"
  },
  "sourceName": "ConsoleApp",
  "preferNameDirectory": true,
  "primaryOutputs": [
    { "path": "src/ConsoleApp.Cli/ConsoleApp.Cli.csproj" }
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

- [ ] **Step 2: Verify**

```bash
cat "/c/Users/ryan7/programming/dotnet-console-template/content/.template.config/template.json"
```

Expected: `"shortName": "cli"`, `"sourceName": "ConsoleApp"`, single primaryOutput.

---

## Task 9: Confirm `Directory.Build.props` is correctly bulk-renamed

**Files:**
- Inspect: `content/Directory.Build.props`

The bulk rename in Task 4 should already have changed the project-name guard from `'$(MSBuildProjectName)' != 'GlobalRealEstate.Analyzers'` to `'$(MSBuildProjectName)' != 'ConsoleApp.Analyzers'`. This task is a verification step — no edits expected.

- [ ] **Step 1: Read the file**

```bash
cat "/c/Users/ryan7/programming/dotnet-console-template/content/Directory.Build.props"
```

Expected: two occurrences of `'$(MSBuildProjectName)' != 'ConsoleApp.Analyzers'`, the analyzer ProjectReference path now points at `src\ConsoleApp.Analyzers\ConsoleApp.Analyzers.csproj`.

- [ ] **Step 2: Spot-check no GlobalRealEstate strings**

```bash
grep "GlobalRealEstate" "/c/Users/ryan7/programming/dotnet-console-template/content/Directory.Build.props"
```

Expected: empty output. If non-empty, re-run Task 4 step 1.

---

## Task 10: Edit `LayerDependencyTests.cs`

**Files:**
- Modify: `content/tests/ConsoleApp.Tests.Architecture/LayerDependencyTests.cs`

Drop the two Api/Etl-specific tests; add one Cli-only test.

- [ ] **Step 1: Overwrite the file**

Path: `content/tests/ConsoleApp.Tests.Architecture/LayerDependencyTests.cs`

```csharp
using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace ConsoleApp.Tests.Architecture;

[TestFixture]
public class LayerDependencyTests
{
    [Test]
    public void Should_keep_models_free_of_data_layer_dependencies()
    {
        Types.InAssembly(TestHelpers.CoreAssembly)
            .That().ResideInNamespaceContaining("Core.Models")
            .ShouldNot().HaveDependencyOnAny(
                "ConsoleApp.Core.Data",
                "Microsoft.EntityFrameworkCore")
            .GetResult().IsSuccessful.Should().BeTrue(
                "Domain models must be pure records with no EF or data layer dependencies");
    }

    [Test]
    public void Should_keep_interfaces_free_of_data_implementation_dependencies()
    {
        Types.InAssembly(TestHelpers.CoreAssembly)
            .That().ResideInNamespaceContaining("Core.Interfaces")
            .ShouldNot().HaveDependencyOnAny(
                "ConsoleApp.Core.Data",
                "Microsoft.EntityFrameworkCore")
            .GetResult().IsSuccessful.Should().BeTrue(
                "Interfaces must not depend on concrete data implementations");
    }

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
}
```

- [ ] **Step 2: Verify**

```bash
grep -c "Test\]" "/c/Users/ryan7/programming/dotnet-console-template/content/tests/ConsoleApp.Tests.Architecture/LayerDependencyTests.cs"
```

Expected: `3`

---

## Task 11: Edit `TestHelpers.cs`

**Files:**
- Modify: `content/tests/ConsoleApp.Tests.Architecture/TestHelpers.cs`

Replace `EtlAssembly` and `ApiAssembly` properties with a single `CliAssembly`. Update the `TestAssemblies` array to drop the second entry-point assembly (no Api). Keep `IsRecord`, `IsDbContext`, the EFCore `using`, and the `ServiceNamespaces` array (parity-preserving per spec).

- [ ] **Step 1: Overwrite the file**

Path: `content/tests/ConsoleApp.Tests.Architecture/TestHelpers.cs`

```csharp
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ConsoleApp.Tests.Architecture;

internal static class TestHelpers
{
    public static Assembly CoreAssembly => typeof(Core.AssemblyMarker).Assembly;
    public static Assembly CliAssembly => typeof(Cli.AssemblyMarker).Assembly;

    public static readonly Assembly[] TestAssemblies =
    [
        typeof(LayerDependencyTests).Assembly,
        typeof(Tests.Unit.AssemblyMarker).Assembly,
        typeof(Tests.Integration.AssemblyMarker).Assembly,
        typeof(Tests.Analyzers.NoTupleReturnAnalyzerTests).Assembly
    ];

    public static readonly string[] ServiceNamespaces =
    [
        "ConsoleApp.Core.Data"
    ];

    public static bool IsRecord(Type type) =>
        type.GetMethod("<Clone>$") != null;

    public static bool IsDbContext(Type type) =>
        typeof(DbContext).IsAssignableFrom(type);
}
```

- [ ] **Step 2: Verify no Etl/Api references remain in test architecture sources**

```bash
grep -E "(EtlAssembly|ApiAssembly|ConsoleApp\.Api|ConsoleApp\.Etl)" \
  "/c/Users/ryan7/programming/dotnet-console-template/content/tests/ConsoleApp.Tests.Architecture/"*.cs
```

Expected: empty output.

---

## Task 12: Edit `setup.ps1` commit message

**Files:**
- Modify: `content/setup.ps1`

The bulk rename of `etl-api` → `cli` in Task 5 should already have updated the commit message line. Verify, no edits expected.

- [ ] **Step 1: Read line 33**

```bash
grep -n "Initial scaffold" "/c/Users/ryan7/programming/dotnet-console-template/content/setup.ps1"
```

Expected output:

```
33:Invoke-Git commit -q --no-verify -m "Initial scaffold from cli template"
```

If it still says `etl-api`, edit the file to change it to `cli`.

---

## Task 13: Edit `content/CLAUDE.md` — architecture section

**Files:**
- Modify: `content/CLAUDE.md`

The bulk rename has already replaced `GlobalRealEstate` with `ConsoleApp` everywhere. The architecture bullet still describes the etl-api shape ("four `src/` projects (`Core`, `Api`, `Etl`, `Analyzers`)") and needs rewriting to the cli shape.

- [ ] **Step 1: Replace the architecture bullet**

In `content/CLAUDE.md`, find the bullet starting `- **Solution:** ConsoleApp.slnx, .NET 10, four src/ projects` (this is what it should say after Task 4's rename).

Replace this single line:

```markdown
- **Solution:** `ConsoleApp.slnx`, .NET 10, four `src/` projects (`Core`, `Api`, `Etl`, `Analyzers`) and four `tests/` projects (`Tests.Unit`, `Tests.Integration`, `Tests.Architecture`, `Tests.Analyzers`).
```

with:

```markdown
- **Solution:** `ConsoleApp.slnx`, .NET 10, three `src/` projects (`Core`, `Cli`, `Analyzers`) and four `tests/` projects (`Tests.Unit`, `Tests.Integration`, `Tests.Architecture`, `Tests.Analyzers`).
```

- [ ] **Step 2: Verify**

```bash
grep -E "(Api|Etl)" "/c/Users/ryan7/programming/dotnet-console-template/content/CLAUDE.md"
```

Expected: empty output. Any remaining mention of `Api` or `Etl` is leftover etl-api framing that needs removing.

---

## Task 14: Edit `content/README.md` — description text

**Files:**
- Modify: `content/README.md`

The bulk renames replaced `GlobalRealEstate` and `etl-api`. Two remaining issues: the description still says "ETL + API solution" and the heading inherits the renamed `# ConsoleApp` (which is fine — it gets replaced at scaffold time by `sourceName`).

- [ ] **Step 1: Read the current state**

```bash
cat "/c/Users/ryan7/programming/dotnet-console-template/content/README.md"
```

- [ ] **Step 2: Overwrite the file**

Path: `content/README.md`

```markdown
# ConsoleApp

Console CLI solution scaffolded from the [cli template](https://github.com/ryan75195/dotnet-cli-template).

## First-time setup

After scaffolding (`dotnet new cli -n ConsoleApp`), run once:

```powershell
.\setup.ps1
```

This initializes a git repo, activates `.githooks/` for the project lifecycle, and creates the initial commit.

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

Direct commits to `main` are blocked. Edits to already-merged branches are blocked.
```

- [ ] **Step 3: Verify**

```bash
grep -E "(ETL|API|GlobalRealEstate|etl-api)" "/c/Users/ryan7/programming/dotnet-console-template/content/README.md"
```

Expected: empty output.

---

## Task 15: Edit top-level `README.md`

**Files:**
- Modify: `README.md`

The bulk renames already updated `GlobalRealEstate` and `etl-api`. The descriptive copy (project counts, "ETL + API" framing) needs an explicit rewrite to match the new shape.

- [ ] **Step 1: Overwrite the file**

Path: `README.md`

```markdown
# dotnet-cli-template

A `dotnet new` template for .NET 10 console / CLI projects. Scaffolds:

- 3 source projects: `Core`, `Cli`, `Analyzers`
- 4 test projects: `Tests.Unit`, `Tests.Integration`, `Tests.Architecture`, `Tests.Analyzers`
- 13 custom Roslyn analyzers (CI0001–CI0013) enforcing project-agnostic code rules
- Architecture tests (NetArchTest + reflection) split across 5 focused fixtures
- Three git/Claude hooks enforcing the development lifecycle
- A `CLAUDE.md` documenting the lifecycle for the scaffolded project

This is the single-entry-point sibling of [dotnet-etl-api-template](https://github.com/ryan75195/dotnet-etl-api-template). When you need an API + worker pair, use that one.

## Install

`dotnet new install` doesn't accept git URLs — clone first, then install from the local `content/` directory:

```powershell
git clone https://github.com/ryan75195/dotnet-cli-template
dotnet new install .\dotnet-cli-template\content
```

## Use

```powershell
dotnet new cli -n MyTool
cd MyTool
.\setup.ps1
```

`MyTool` becomes the project name everywhere — namespaces, project files, the `.slnx`.

After `setup.ps1`:
- git repo is initialized
- `.githooks/` is active
- initial commit is in
- ready for `gh repo create` and the first issue

## Update

```powershell
cd dotnet-cli-template
git pull
dotnet new install .\content --force
```

## Uninstall

```powershell
dotnet new uninstall <path-you-used-at-install-time>\content
```

(For example, `dotnet new uninstall .\dotnet-cli-template\content`.)

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
```

- [ ] **Step 2: Verify**

```bash
grep -E "(ETL|GlobalRealEstate|etl-api)" "/c/Users/ryan7/programming/dotnet-console-template/README.md"
```

Expected output should only contain the one line that *intentionally* references etl-api as the sibling repo:

```
This is the single-entry-point sibling of [dotnet-etl-api-template]...
```

Any other matches mean the rewrite missed something.

---

## Task 16: Edit `.github/workflows/template-ci.yml`

**Files:**
- Modify: `.github/workflows/template-ci.yml`

The bulk rename in Task 5 already changed `etl-api` → `cli`. Verify, no edits expected.

- [ ] **Step 1: Read the file**

```bash
cat "/c/Users/ryan7/programming/dotnet-console-template/.github/workflows/template-ci.yml"
```

Expected: line `run: dotnet new cli -n ScaffoldTest -o ${{ runner.temp }}/scaffold-test`

- [ ] **Step 2: Verify**

```bash
grep "etl-api" "/c/Users/ryan7/programming/dotnet-console-template/.github/workflows/template-ci.yml"
```

Expected: empty output.

---

## Task 17: Edit `template-tests/scaffold-and-build.ps1`

**Files:**
- Modify: `template-tests/scaffold-and-build.ps1`

The bulk rename already changed `etl-api-template-smoke` → `cli-template-smoke` and `dotnet new etl-api` → `dotnet new cli`. Verify, no edits expected.

- [ ] **Step 1: Read the file**

```bash
cat "/c/Users/ryan7/programming/dotnet-console-template/template-tests/scaffold-and-build.ps1"
```

Expected: `$scaffoldDir = Join-Path $env:TEMP "cli-template-smoke"` and `dotnet new cli -n SmokeTest -o $scaffoldDir`.

- [ ] **Step 2: Verify**

```bash
grep "etl-api" "/c/Users/ryan7/programming/dotnet-console-template/template-tests/scaffold-and-build.ps1"
```

Expected: empty output.

---

## Task 18: Run the smoke test locally

**Files:** none modified — this is a validation-only task.

Runs `template-tests/scaffold-and-build.ps1`, which installs the template from `./content`, scaffolds a project named `SmokeTest` into `$env:TEMP\cli-template-smoke`, builds it, runs all 4 test projects, and uninstalls on exit.

- [ ] **Step 1: Run the script**

```powershell
cd C:\Users\ryan7\programming\dotnet-console-template
.\template-tests\scaffold-and-build.ps1
```

Expected: no thrown exceptions; final output ends with `Done.`. Build succeeds. Architecture, Unit, Integration, and Analyzers test projects all pass.

- [ ] **Step 2: If the script fails, diagnose**

Common failure modes and where to look:

| Symptom | Likely cause | Where to look |
|---|---|---|
| `dotnet new cli` not found | template.json `shortName` not `cli` | Task 8 |
| Build failure: `CS0246: type or namespace 'Cli' could not be found` | `TestHelpers.cs` Cli reference path | Task 11 step 1 |
| LayerDependencyTests fails: cannot load `ConsoleApp.Cli` | Cli csproj missing or has wrong assembly name | Task 6 step 2 |
| Some analyzer test fails | Bulk rename touched a string-literal-sensitive analyzer test fixture | Re-grep `GlobalRealEstate` under tests/Analyzers |
| `.githooks/` script error during scaffolded build | Hooks reference old project name | Re-grep `GlobalRealEstate` under content/.githooks |

Re-run the script after each fix. Do **not** mark this task complete until the script exits with `Done.` and `$LASTEXITCODE` is 0.

- [ ] **Step 3: Confirm exit status**

```powershell
echo $LASTEXITCODE
```

Expected: `0`

---

## Task 19: Commit the scaffold

**Files:** none modified — this is a commit-only task.

- [ ] **Step 1: Stage all template files**

```bash
cd "/c/Users/ryan7/programming/dotnet-console-template" && \
  git add -A && \
  git status --short | wc -l
```

Expected: a non-zero count (lots of staged files from the copy + transformations).

- [ ] **Step 2: Commit**

```bash
cd "/c/Users/ryan7/programming/dotnet-console-template" && \
  git commit -q -m "Scaffold dotnet-console-template from etl-api template

Single console entry point (Cli) sharing Core + Analyzers, with the same
guardrails: 13 analyzers, 5 architecture-test fixtures, git/Claude hooks,
template-CI workflow, setup.ps1." && \
  git log --oneline
```

Expected:

```
<sha> Scaffold dotnet-console-template from etl-api template
<sha> Add design spec for dotnet-console-template
```

- [ ] **Step 3: Final verification**

```bash
cd "/c/Users/ryan7/programming/dotnet-console-template" && \
  git status
```

Expected: `nothing to commit, working tree clean`.

---

## Done

The template is now installable locally:

```powershell
dotnet new install C:\Users\ryan7\programming\dotnet-console-template\content
dotnet new cli -n MyTool
```

Next steps the user may want to take (out of scope for this plan):
- `gh repo create ryan75195/dotnet-cli-template --public --source . --push`
- Add a link from `dotnet-etl-api-template`'s README pointing at the new template
