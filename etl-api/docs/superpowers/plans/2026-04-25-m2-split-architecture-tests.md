# M2 Split Architecture Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the 636-line `ArchitectureTests.cs` (20 tests) into 5 focused test files plus a shared `TestHelpers.cs`, with no behavior change.

**Architecture:** Mechanical refactor. Group tests by purpose; extract shared infrastructure into `TestHelpers`; keep fixture-specific helpers (regex patterns, allow-lists) co-located with their single consumer. Each commit leaves the test suite green.

**Tech Stack:** C# / .NET 10, NUnit, FluentAssertions, NetArchTest.

**Spec:** `docs/superpowers/specs/2026-04-25-etl-api-template-extraction-design.md` (M2 section)
**Umbrella issue:** #13

---

## File map

**Modify:**
- `tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs` — progressively shrunk; deleted in Task 7

**Create:**
- `tests/GlobalRealEstate.Tests.Architecture/TestHelpers.cs` — shared assembly accessors + helpers
- `tests/GlobalRealEstate.Tests.Architecture/LayerDependencyTests.cs` — 4 tests
- `tests/GlobalRealEstate.Tests.Architecture/NamingConventionTests.cs` — 8 tests
- `tests/GlobalRealEstate.Tests.Architecture/ServiceShapeTests.cs` — 3 tests
- `tests/GlobalRealEstate.Tests.Architecture/CodeStructureTests.cs` — 4 tests
- `tests/GlobalRealEstate.Tests.Architecture/DiRegistrationTests.cs` — 1 test

---

## Helper allocation strategy

**`TestHelpers` (shared by 2+ fixtures):**
- `CoreAssembly`, `EtlAssembly`, `ApiAssembly` — used by every fixture
- `TestAssemblies` array — used by Naming + CodeStructure
- `ServiceNamespaces` — used by Service + Naming
- `IsRecord(Type)`, `IsDbContext(Type)` — used by Service + Naming + CodeStructure

**Co-located with single consumer (kept private to its fixture):**
- `TestMethodPattern` regex → `NamingConventionTests`
- `FrameworkAsyncMethods` set → `NamingConventionTests`
- `AllowedSuffixes` set → `NamingConventionTests`
- `AllowedConcreteParams` set → `ServiceShapeTests`
- `AllowedArrayReturns` set → `CodeStructureTests`
- `PublicTypePattern` regex → `CodeStructureTests`
- `FindSolutionRoot()` method → `CodeStructureTests`

---

## Cleanup-during-move

Line 56 of the current `ArchitectureTests.cs` has a leftover reference to `"GlobalRealEstate.Core.Scraping"` inside `Should_keep_interfaces_free_of_data_implementation_dependencies`'s `HaveDependencyOnAny` call. M1's spec only flagged the parallel test for models. The `Scraping` namespace doesn't exist, so the assertion always passes — it's dead but not broken. Drop it when extracting this test in Task 3.

---

## Pre-flight

- [ ] **Step 0a: Confirm starting branch is `main` and up-to-date**

Run:
```bash
git checkout main && git pull --ff-only
```

Expected: clean fast-forward (or already up-to-date).

- [ ] **Step 0b: Confirm baseline test counts**

Run:
```bash
dotnet build && dotnet test --no-build --verbosity minimal
```

Expected per-project counts:
- `GlobalRealEstate.Tests.Architecture`: **Passed: 20**
- `GlobalRealEstate.Tests.Analyzers`: **Passed: 70**
- `GlobalRealEstate.Tests.Unit`: **Passed: 1**
- `GlobalRealEstate.Tests.Integration`: **Passed: 1**

If any number differs, **stop** and investigate. M2 expects M1 already merged.

---

## Task 1: Open issue + create feat branch

**Files:** none (GitHub + git operations)

- [ ] **Step 1a: Create the M2 issue on GitHub**

Run:
```bash
gh issue create --title "Split ArchitectureTests.cs into focused fixture files" --body "Part of #13 (template extraction).

The 636-line ArchitectureTests.cs holds 20 tests covering distinct concerns. Split into TestHelpers.cs + 5 focused fixtures (LayerDependencyTests, NamingConventionTests, ServiceShapeTests, CodeStructureTests, DiRegistrationTests). Mechanical refactor — no behavior change.

Plan: docs/superpowers/plans/2026-04-25-m2-split-architecture-tests.md
Spec: docs/superpowers/specs/2026-04-25-etl-api-template-extraction-design.md"
```

Expected: prints issue URL. **Capture the issue number** as `<N>`.

- [ ] **Step 1b: Create the feat branch**

Run (substitute `<N>`):
```bash
git checkout -b feat/<N>-split-architecture-tests
```

Expected: switches to new branch, `reference-transaction` hook confirms the issue.

---

## Task 2: Create `TestHelpers.cs`; refactor `ArchitectureTests.cs` to use it

**Files:**
- Create: `tests/GlobalRealEstate.Tests.Architecture/TestHelpers.cs`
- Modify: `tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs` — replace local declarations with `TestHelpers.X` references

- [ ] **Step 2a: Create `TestHelpers.cs`**

Write to `tests/GlobalRealEstate.Tests.Architecture/TestHelpers.cs`:

```csharp
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace GlobalRealEstate.Tests.Architecture;

internal static class TestHelpers
{
    public static Assembly CoreAssembly => typeof(Core.AssemblyMarker).Assembly;
    public static Assembly EtlAssembly => typeof(Etl.AssemblyMarker).Assembly;
    public static Assembly ApiAssembly => typeof(Api.AssemblyMarker).Assembly;

    public static readonly Assembly[] TestAssemblies =
    [
        typeof(ArchitectureTests).Assembly,
        typeof(Tests.Unit.AssemblyMarker).Assembly,
        typeof(Tests.Integration.AssemblyMarker).Assembly,
        typeof(Tests.Analyzers.NoTupleReturnAnalyzerTests).Assembly
    ];

    public static readonly string[] ServiceNamespaces =
    [
        "GlobalRealEstate.Core.Data"
    ];

    public static bool IsRecord(Type type) =>
        type.GetMethod("<Clone>$") != null;

    public static bool IsDbContext(Type type) =>
        typeof(DbContext).IsAssignableFrom(type);
}
```

`TestAssemblies` still references `typeof(ArchitectureTests)` because that class exists right now. Task 7 flips this to `typeof(LayerDependencyTests)` after `ArchitectureTests` is deleted.

- [ ] **Step 2b: Refactor `ArchitectureTests.cs` to use `TestHelpers`**

Open `tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs`. Delete the following declarations (lines 14-35 of the current file):

```csharp
    private static Assembly CoreAssembly => typeof(Core.AssemblyMarker).Assembly;
    private static Assembly EtlAssembly => typeof(Etl.AssemblyMarker).Assembly;
    private static Assembly ApiAssembly => typeof(Api.AssemblyMarker).Assembly;

    private static readonly string[] ServiceNamespaces =
    [
        "GlobalRealEstate.Core.Data"
    ];

    private static readonly Assembly[] TestAssemblies =
    [
        typeof(ArchitectureTests).Assembly,
        typeof(Tests.Unit.AssemblyMarker).Assembly,
        typeof(Tests.Integration.AssemblyMarker).Assembly,
        typeof(Tests.Analyzers.NoTupleReturnAnalyzerTests).Assembly
    ];

    private static bool IsRecord(Type type) =>
        type.GetMethod("<Clone>$") != null;

    private static bool IsDbContext(Type type) =>
        typeof(DbContext).IsAssignableFrom(type);
```

Now also delete the `FindSolutionRoot()` method at the bottom of the file (lines 606-620 of the current file) — wait, **don't**. `FindSolutionRoot` is only used by `Should_declare_at_most_one_public_type_per_file`, which stays in `ArchitectureTests.cs` until Task 6 extracts it to `CodeStructureTests`. Leave `FindSolutionRoot` private to `ArchitectureTests` for now; it moves with that test in Task 6.

In every test method that referenced the deleted symbols, prefix with `TestHelpers.`:
- `CoreAssembly` → `TestHelpers.CoreAssembly`
- `EtlAssembly` → `TestHelpers.EtlAssembly`
- `ApiAssembly` → `TestHelpers.ApiAssembly`
- `ServiceNamespaces` → `TestHelpers.ServiceNamespaces`
- `TestAssemblies` → `TestHelpers.TestAssemblies`
- `IsRecord(...)` → `TestHelpers.IsRecord(...)`
- `IsDbContext(...)` → `TestHelpers.IsDbContext(...)`

These references appear ~30+ times across the file. Use Edit's `replace_all` for each token, or grep+verify if doing manually:
```bash
grep -nE "\b(CoreAssembly|EtlAssembly|ApiAssembly|ServiceNamespaces|TestAssemblies|IsRecord|IsDbContext)\b" tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs
```

Every match (other than in `using` lines) must be prefixed.

Also remove the `using Microsoft.EntityFrameworkCore;` from `ArchitectureTests.cs` if `IsDbContext` was the only consumer — actually, no: `Microsoft.EntityFrameworkCore` is also referenced in `HaveDependencyOnAny("...EntityFrameworkCore")` strings as text literals, so keep the using if the file still has actual usages. Run the build to check.

- [ ] **Step 2c: Build**

Run:
```bash
dotnet build tests/GlobalRealEstate.Tests.Architecture/
```

Expected: `Build succeeded`. If any unresolved-symbol errors, find the missed reference and prefix with `TestHelpers.`.

- [ ] **Step 2d: Run tests; expect 20 passed**

Run:
```bash
dotnet test tests/GlobalRealEstate.Tests.Architecture/ --no-build --verbosity minimal
```

Expected: `Passed: 20`.

- [ ] **Step 2e: Commit**

```bash
git add tests/GlobalRealEstate.Tests.Architecture/
git commit -m "$(cat <<'EOF'
refactor: extract TestHelpers from ArchitectureTests

Pulls shared assembly accessors, ServiceNamespaces, IsRecord/IsDbContext
into a TestHelpers static class. ArchitectureTests now references via
TestHelpers.X. No behavior change.

First step in splitting ArchitectureTests.cs into focused fixture files.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

Expected: pre-commit passes (build, format, 20 architecture + 1 unit smoke = 21 tests).

---

## Task 3: Extract `LayerDependencyTests.cs`

**Files:**
- Create: `tests/GlobalRealEstate.Tests.Architecture/LayerDependencyTests.cs`
- Modify: `tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs` — remove the 4 layer tests

The 4 tests being moved:
- `Should_keep_models_free_of_data_layer_dependencies`
- `Should_keep_interfaces_free_of_data_implementation_dependencies` (drop the dead `"GlobalRealEstate.Core.Scraping"` argument while moving)
- `Should_keep_api_free_of_etl_references`
- `Should_keep_etl_free_of_api_dependencies`

- [ ] **Step 3a: Create `LayerDependencyTests.cs`**

Write to `tests/GlobalRealEstate.Tests.Architecture/LayerDependencyTests.cs`:

```csharp
using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace GlobalRealEstate.Tests.Architecture;

[TestFixture]
public class LayerDependencyTests
{
    [Test]
    public void Should_keep_models_free_of_data_layer_dependencies()
    {
        Types.InAssembly(TestHelpers.CoreAssembly)
            .That().ResideInNamespaceContaining("Core.Models")
            .ShouldNot().HaveDependencyOnAny(
                "GlobalRealEstate.Core.Data",
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
                "GlobalRealEstate.Core.Data",
                "Microsoft.EntityFrameworkCore")
            .GetResult().IsSuccessful.Should().BeTrue(
                "Interfaces must not depend on concrete data implementations");
    }

    [Test]
    public void Should_keep_api_free_of_etl_references()
    {
        var apiAssembly = Assembly.Load("GlobalRealEstate.Api");
        var referencedNames = apiAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        referencedNames.Should().NotContain("GlobalRealEstate.Etl",
            "API layer must not reference ETL — they are independent entry points");
    }

    [Test]
    public void Should_keep_etl_free_of_api_dependencies()
    {
        Types.InAssembly(TestHelpers.EtlAssembly)
            .That().ResideInNamespaceContaining("Etl")
            .ShouldNot().HaveDependencyOnAny("GlobalRealEstate.Api")
            .GetResult().IsSuccessful.Should().BeTrue(
                "ETL pipeline must not reference the API layer");
    }
}
```

- [ ] **Step 3b: Remove the 4 tests from `ArchitectureTests.cs`**

Delete the `[Test] public void Should_keep_models_free_of_data_layer_dependencies()` method, the `[Test] public void Should_keep_interfaces_free_of_data_implementation_dependencies()` method, the `[Test] public void Should_keep_api_free_of_etl_references()` method, and the `[Test] public void Should_keep_etl_free_of_api_dependencies()` method (with their attributes and trailing blank lines).

- [ ] **Step 3c: Build + run tests**

```bash
dotnet build && dotnet test tests/GlobalRealEstate.Tests.Architecture/ --no-build --verbosity minimal
```

Expected: `Passed: 20` (4 moved, total unchanged).

- [ ] **Step 3d: Format + commit**

If `dotnet format` has anything to fix (line endings on the new file), let it run, then:

```bash
dotnet format
git add tests/GlobalRealEstate.Tests.Architecture/
git commit -m "$(cat <<'EOF'
refactor: extract LayerDependencyTests from ArchitectureTests

Four layer-boundary tests pulled into their own fixture. Also drops a
dead 'Core.Scraping' reference from the interface-deps test (namespace
doesn't exist; assertion was a no-op).

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

Expected: pre-commit passes.

---

## Task 4: Extract `NamingConventionTests.cs`

**Files:**
- Create: `tests/GlobalRealEstate.Tests.Architecture/NamingConventionTests.cs`
- Modify: `tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs` — remove the 8 naming tests + `TestMethodPattern`, `FrameworkAsyncMethods`, `AllowedSuffixes`

The 8 tests being moved:
- `Should_require_record_types_in_core_models`
- `Should_match_interface_naming_suffix_on_implementations`
- `Should_end_with_entity_for_types_in_entities_namespace`
- `Should_end_with_tests_for_all_test_fixtures`
- `Should_follow_naming_convention_for_all_test_methods`
- `Should_not_use_async_suffix_on_method_names`
- `Should_use_recognised_role_suffix_on_concrete_classes`
- `Should_place_interfaces_in_interfaces_namespace`

Plus the helpers `TestMethodPattern`, `FrameworkAsyncMethods`, `AllowedSuffixes`.

- [ ] **Step 4a: Create `NamingConventionTests.cs`**

Write to `tests/GlobalRealEstate.Tests.Architecture/NamingConventionTests.cs`:

```csharp
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace GlobalRealEstate.Tests.Architecture;

[TestFixture]
public class NamingConventionTests
{
    private static readonly Regex TestMethodPattern = new(
        @"^Should(_[a-z0-9]+)+$", RegexOptions.Compiled);

    private static readonly HashSet<string> FrameworkAsyncMethods =
    [
        "DisposeAsync",
        "ExecuteAsync",
        "StartAsync",
        "StopAsync"
    ];

    private static readonly HashSet<string> AllowedSuffixes =
    [
        "Service", "Repository", "Client", "Store", "Context",
        "Entity", "Command", "Parser", "Converter", "Pool",
        "Worker", "Process", "Extensions", "Mapper", "Extractor",
        "Probe", "Result", "Monitor", "Plugin", "Filter"
    ];

    [Test]
    public void Should_require_record_types_in_core_models()
    {
        var modelTypes = TestHelpers.CoreAssembly.GetTypes()
            .Where(t => t.IsPublic
                && t.Namespace == "GlobalRealEstate.Core.Models")
            .ToList();

        Assert.Multiple(() =>
        {
            foreach (var type in modelTypes)
            {
                TestHelpers.IsRecord(type).Should().BeTrue(
                    $"{type.Name} in Core.Models must be a record for immutability");
            }
        });
    }

    [Test]
    public void Should_match_interface_naming_suffix_on_implementations()
    {
        var serviceTypes = TestHelpers.CoreAssembly.GetTypes()
            .Where(t => t.IsClass
                && t.IsPublic
                && !t.IsAbstract
                && TestHelpers.ServiceNamespaces.Contains(t.Namespace)
                && !TestHelpers.IsRecord(t)
                && !TestHelpers.IsDbContext(t))
            .ToList();

        Assert.Multiple(() =>
        {
            foreach (var type in serviceTypes)
            {
                var baseInterfaces2 = type.BaseType?.GetInterfaces() ?? [];
                var projectInterfaces = type.GetInterfaces()
                    .Except(baseInterfaces2)
                    .Where(i => i.Namespace?.StartsWith("GlobalRealEstate", StringComparison.Ordinal) == true);

                foreach (var iface in projectInterfaces)
                {
                    var expectedSuffix = iface.Name[1..];
                    type.Name.Should().EndWith(expectedSuffix,
                        $"{type.Name} implements {iface.Name} so should end with '{expectedSuffix}'");
                }
            }
        });
    }

    [Test]
    public void Should_end_with_entity_for_types_in_entities_namespace()
    {
        var entityTypes = TestHelpers.CoreAssembly.GetTypes()
            .Where(t => t.IsClass
                && t.IsPublic
                && t.Namespace == "GlobalRealEstate.Core.Data.Entities")
            .ToList();

        Assert.Multiple(() =>
        {
            foreach (var type in entityTypes)
            {
                type.Name.Should().EndWith("Entity",
                    $"{type.Name} in Data.Entities namespace must end with 'Entity'");
            }
        });
    }

    [Test]
    public void Should_end_with_tests_for_all_test_fixtures()
    {
        var testFixtures = TestHelpers.TestAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass
                && t.IsPublic
                && t.GetCustomAttributes(typeof(TestFixtureAttribute), inherit: false).Length > 0)
            .ToList();

        testFixtures.Should().NotBeEmpty("we expect test fixtures across test projects");

        Assert.Multiple(() =>
        {
            foreach (var type in testFixtures)
            {
                type.Name.Should().EndWith("Tests",
                    $"{type.Name} is a [TestFixture] and must end with 'Tests'");
            }
        });
    }

    [Test]
    public void Should_follow_naming_convention_for_all_test_methods()
    {
        var testAttribute = typeof(TestAttribute);
        var testCaseAttribute = typeof(TestCaseAttribute);

        var testMethods = TestHelpers.TestAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && t.IsPublic)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => m.GetCustomAttributes(testAttribute, inherit: false).Length > 0
                || m.GetCustomAttributes(testCaseAttribute, inherit: false).Length > 0)
            .ToList();

        testMethods.Should().NotBeEmpty("we expect test methods across test projects");

        var violations = testMethods
            .Where(m => !TestMethodPattern.IsMatch(m.Name))
            .Select(m => $"  {m.DeclaringType?.Name}.{m.Name}")
            .ToList();

        violations.Should().BeEmpty(
            $"Test methods must follow Should_word_word pattern. Violations ({violations.Count}):\n{string.Join("\n", violations)}");
    }

    [Test]
    public void Should_not_use_async_suffix_on_method_names()
    {
        var assemblies = new[] { TestHelpers.CoreAssembly, TestHelpers.EtlAssembly, TestHelpers.ApiAssembly };

        var violations = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsPublic)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal)
                && !FrameworkAsyncMethods.Contains(m.Name))
            .Select(m => $"  {m.DeclaringType?.Name}.{m.Name}")
            .Distinct()
            .ToList();

        violations.Should().BeEmpty(
            $"Methods must not use Async suffix. Violations ({violations.Count}):\n{string.Join("\n", violations)}");
    }

    [Test]
    public void Should_use_recognised_role_suffix_on_concrete_classes()
    {
        var assemblies = new[] { TestHelpers.CoreAssembly, TestHelpers.EtlAssembly, TestHelpers.ApiAssembly };

        var concreteClasses = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass
                && t.IsPublic
                && !t.IsAbstract
                && !TestHelpers.IsRecord(t)
                && !TestHelpers.IsDbContext(t)
                && !t.Name.StartsWith('<')
                && t.Name != "Program")
            .ToList();

        var violations = concreteClasses
            .Where(t => !AllowedSuffixes.Any(s => t.Name.EndsWith(s, StringComparison.Ordinal)))
            .Select(t => $"  {t.FullName}")
            .ToList();

        violations.Should().BeEmpty(
            $"Concrete classes must end with a recognised role suffix ({string.Join(", ", AllowedSuffixes)}). " +
            $"Violations ({violations.Count}):\n{string.Join("\n", violations)}");
    }

    [Test]
    public void Should_place_interfaces_in_interfaces_namespace()
    {
        var assemblies = new[] { TestHelpers.CoreAssembly, TestHelpers.EtlAssembly, TestHelpers.ApiAssembly };

        var projectInterfaces = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsInterface
                && t.IsPublic
                && t.Namespace?.StartsWith("GlobalRealEstate", StringComparison.Ordinal) == true)
            .ToList();

        var violations = projectInterfaces
            .Where(t => !t.Namespace!.EndsWith(".Interfaces", StringComparison.Ordinal))
            .Select(t => $"  {t.FullName} is in {t.Namespace}")
            .ToList();

        violations.Should().BeEmpty(
            $"Interfaces must reside in an .Interfaces namespace. " +
            $"Violations ({violations.Count}):\n{string.Join("\n", violations)}");
    }
}
```

- [ ] **Step 4b: Remove the 8 tests + their helpers from `ArchitectureTests.cs`**

In `ArchitectureTests.cs`, delete:
- The 8 `[Test] public void` methods listed above
- The 3 helper declarations: `private static readonly Regex TestMethodPattern = ...`, `private static readonly HashSet<string> FrameworkAsyncMethods = ...`, `private static readonly HashSet<string> AllowedSuffixes = ...`

- [ ] **Step 4c: Build + test**

```bash
dotnet build && dotnet test tests/GlobalRealEstate.Tests.Architecture/ --no-build --verbosity minimal
```

Expected: `Passed: 20`.

- [ ] **Step 4d: Format + commit**

```bash
dotnet format
git add tests/GlobalRealEstate.Tests.Architecture/
git commit -m "$(cat <<'EOF'
refactor: extract NamingConventionTests from ArchitectureTests

Eight naming-related tests and three single-consumer helpers
(TestMethodPattern, FrameworkAsyncMethods, AllowedSuffixes) moved
into their own fixture.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

Expected: pre-commit passes.

---

## Task 5: Extract `ServiceShapeTests.cs`

**Files:**
- Create: `tests/GlobalRealEstate.Tests.Architecture/ServiceShapeTests.cs`
- Modify: `tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs` — remove the 3 service-shape tests + `AllowedConcreteParams`

The 3 tests being moved:
- `Should_require_interfaces_on_core_service_classes`
- `Should_inject_dependencies_as_interfaces_not_concrete_types`
- `Should_not_allow_static_classes_in_commands_namespace`

- [ ] **Step 5a: Create `ServiceShapeTests.cs`**

Write to `tests/GlobalRealEstate.Tests.Architecture/ServiceShapeTests.cs`:

```csharp
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace GlobalRealEstate.Tests.Architecture;

[TestFixture]
public class ServiceShapeTests
{
    private static readonly HashSet<Type> AllowedConcreteParams =
    [
        typeof(string),
        typeof(System.IO.StreamReader),
        typeof(System.IO.StreamWriter),
        typeof(HttpClient),
        typeof(TimeProvider),
    ];

    [Test]
    public void Should_require_interfaces_on_core_service_classes()
    {
        var serviceTypes = TestHelpers.CoreAssembly.GetTypes()
            .Where(t => t.IsClass
                && t.IsPublic
                && !t.IsAbstract
                && TestHelpers.ServiceNamespaces.Contains(t.Namespace)
                && !TestHelpers.IsRecord(t)
                && !TestHelpers.IsDbContext(t))
            .ToList();

        Assert.Multiple(() =>
        {
            foreach (var type in serviceTypes)
            {
                var baseInterfaces = type.BaseType?.GetInterfaces() ?? [];
                var ownInterfaces = type.GetInterfaces().Except(baseInterfaces);

                ownInterfaces.Should().NotBeEmpty(
                    $"{type.Name} must implement an interface (Dependency Inversion Principle)");
            }
        });
    }

    [Test]
    public void Should_inject_dependencies_as_interfaces_not_concrete_types()
    {
        var assemblies = new[] { TestHelpers.CoreAssembly, TestHelpers.EtlAssembly, TestHelpers.ApiAssembly };

        var serviceClasses = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass
                && t.IsPublic
                && !t.IsAbstract
                && !TestHelpers.IsRecord(t)
                && !TestHelpers.IsDbContext(t)
                && t.Namespace?.Contains(".Entities", StringComparison.Ordinal) != true)
            .ToList();

        var violations = new List<string>();

        foreach (var type in serviceClasses)
        {
            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctor in ctors)
            {
                foreach (var param in ctor.GetParameters())
                {
                    var paramType = param.ParameterType;

                    if (paramType.IsInterface)
                    {
                        continue;
                    }

                    if (paramType.IsValueType)
                    {
                        continue;
                    }

                    if (AllowedConcreteParams.Contains(paramType))
                    {
                        continue;
                    }

                    var ns = paramType.Namespace ?? "";
                    if (ns.StartsWith("System.Threading", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (typeof(DbContext).IsAssignableFrom(paramType))
                    {
                        continue;
                    }

                    if (typeof(Delegate).IsAssignableFrom(paramType))
                    {
                        continue;
                    }

                    if (TestHelpers.IsRecord(paramType))
                    {
                        continue;
                    }

                    violations.Add(
                        $"  {type.Name}(… {paramType.Name} {param.Name} …) — should be an interface");
                }
            }
        }

        violations.Should().BeEmpty(
            $"Constructor dependencies must be interfaces, not concrete types. " +
            $"Violations ({violations.Count}):\n{string.Join("\n", violations)}");
    }

    [Test]
    public void Should_not_allow_static_classes_in_commands_namespace()
    {
        var commandTypes = TestHelpers.EtlAssembly.GetTypes()
            .Where(t => t.IsClass
                && t.IsPublic
                && t.Namespace == "GlobalRealEstate.Etl.Commands")
            .ToList();

        var violations = commandTypes
            .Where(t => t.IsAbstract && t.IsSealed)
            .Select(t => t.Name)
            .ToList();

        violations.Should().BeEmpty(
            $"Command classes must not be static — use constructor injection. " +
            $"Violations: {string.Join(", ", violations)}");
    }
}
```

- [ ] **Step 5b: Remove the 3 tests + `AllowedConcreteParams` from `ArchitectureTests.cs`**

- [ ] **Step 5c: Build + test**

```bash
dotnet build && dotnet test tests/GlobalRealEstate.Tests.Architecture/ --no-build --verbosity minimal
```

Expected: `Passed: 20`.

- [ ] **Step 5d: Format + commit**

```bash
dotnet format
git add tests/GlobalRealEstate.Tests.Architecture/
git commit -m "$(cat <<'EOF'
refactor: extract ServiceShapeTests from ArchitectureTests

Three DI-shape tests and AllowedConcreteParams allow-list moved
into their own fixture.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Extract `CodeStructureTests.cs`

**Files:**
- Create: `tests/GlobalRealEstate.Tests.Architecture/CodeStructureTests.cs`
- Modify: `tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs` — remove the 4 structure tests + `AllowedArrayReturns`, `PublicTypePattern`, `FindSolutionRoot`

The 4 tests being moved:
- `Should_have_test_fixture_for_every_public_class`
- `Should_place_test_fixtures_in_namespace_matching_source_assembly`
- `Should_not_return_arrays_from_public_methods`
- `Should_declare_at_most_one_public_type_per_file`

- [ ] **Step 6a: Create `CodeStructureTests.cs`**

Write to `tests/GlobalRealEstate.Tests.Architecture/CodeStructureTests.cs`:

```csharp
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace GlobalRealEstate.Tests.Architecture;

[TestFixture]
public class CodeStructureTests
{
    private static readonly HashSet<string> AllowedArrayReturns = [];

    private static readonly Regex PublicTypePattern = new(
        @"^\s*public\s+(?:(?:static|sealed|abstract|partial|readonly)\s+)*(?:record\s+(?:struct|class)\s+|(?:class|record|struct|enum|interface)\s+)(\w+)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    [Test]
    public void Should_have_test_fixture_for_every_public_class()
    {
        var assemblies = new[] { TestHelpers.CoreAssembly, TestHelpers.EtlAssembly, TestHelpers.ApiAssembly };

        var testFixtureNames = TestHelpers.TestAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && t.IsPublic
                && t.Name.EndsWith("Tests", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToHashSet();

        var classesRequiringTests = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass
                && t.IsPublic
                && !t.IsAbstract
                && !TestHelpers.IsRecord(t)
                && !TestHelpers.IsDbContext(t)
                && t.Namespace?.Contains(".Entities", StringComparison.Ordinal) != true
                && t.Name != "Program")
            .ToList();

        var violations = classesRequiringTests
            .Where(t => !testFixtureNames.Contains(t.Name + "Tests"))
            .Select(t => $"  {t.FullName} — expected {t.Name}Tests")
            .ToList();

        violations.Should().BeEmpty(
            $"Every public class must have a corresponding test fixture. " +
            $"Violations ({violations.Count}):\n{string.Join("\n", violations)}");
    }

    [Test]
    public void Should_place_test_fixtures_in_namespace_matching_source_assembly()
    {
        var sourceAssemblyMap = new Dictionary<string, string>
        {
            ["GlobalRealEstate.Core"] = ".Core",
            ["GlobalRealEstate.Etl"] = ".Etl"
        };

        var sourceAssemblies = new[] { TestHelpers.CoreAssembly, TestHelpers.EtlAssembly, TestHelpers.ApiAssembly };

        var sourceClassToAssembly = new Dictionary<string, string>();
        foreach (var assembly in sourceAssemblies)
        {
            var assemblyName = assembly.GetName().Name!;
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsClass && type.IsPublic && !type.IsAbstract)
                {
                    sourceClassToAssembly.TryAdd(type.Name, assemblyName);
                }
            }
        }

        var unitTestAssembly = typeof(Tests.Unit.AssemblyMarker).Assembly;
        var testFixtures = unitTestAssembly.GetTypes()
            .Where(t => t.IsClass && t.IsPublic
                && t.Name.EndsWith("Tests", StringComparison.Ordinal)
                && t.Name.Length > 5)
            .ToList();

        var violations = new List<string>();

        foreach (var fixture in testFixtures)
        {
            var sourceClassName = fixture.Name[..^5];
            if (!sourceClassToAssembly.TryGetValue(sourceClassName, out var sourceAssemblyName))
            {
                continue;
            }

            if (!sourceAssemblyMap.TryGetValue(sourceAssemblyName, out var requiredSegment))
            {
                continue;
            }

            var fixtureNs = fixture.Namespace ?? "";
            if (!fixtureNs.Contains(requiredSegment, StringComparison.Ordinal))
            {
                violations.Add(
                    $"  {fixture.Name} is in '{fixtureNs}' but tests {sourceAssemblyName}.{sourceClassName}" +
                    $" — move to a namespace containing '{requiredSegment}'");
            }
        }

        violations.Should().BeEmpty(
            $"Test fixtures must be in a sub-namespace matching their source assembly. " +
            $"Violations ({violations.Count}):\n{string.Join("\n", violations)}");
    }

    [Test]
    public void Should_not_return_arrays_from_public_methods()
    {
        var assemblies = new[] { TestHelpers.CoreAssembly, TestHelpers.EtlAssembly, TestHelpers.ApiAssembly };

        var violations = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsPublic)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m =>
            {
                var returnType = m.ReturnType;

                if (returnType.IsGenericType
                    && (returnType.GetGenericTypeDefinition() == typeof(Task<>)
                        || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
                {
                    returnType = returnType.GetGenericArguments()[0];
                }

                return returnType.IsArray
                    && !AllowedArrayReturns.Contains(m.Name);
            })
            .Select(m => $"  {m.DeclaringType?.Name}.{m.Name} returns {m.ReturnType.Name}")
            .Distinct()
            .ToList();

        violations.Should().BeEmpty(
            $"Methods should return IEnumerable<T>/IReadOnlyList<T> instead of T[]. Violations ({violations.Count}):\n{string.Join("\n", violations)}");
    }

    [Test]
    public void Should_declare_at_most_one_public_type_per_file()
    {
        var solutionRoot = FindSolutionRoot();
        var srcDirs = new[]
        {
            Path.Combine(solutionRoot, "src", "GlobalRealEstate.Core"),
            Path.Combine(solutionRoot, "src", "GlobalRealEstate.Etl")
        };

        var violations = new List<string>();

        foreach (var srcDir in srcDirs)
        {
            if (!Directory.Exists(srcDir))
            {
                continue;
            }

            var csFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal));

            foreach (var file in csFiles)
            {
                var content = File.ReadAllText(file);
                var matches = PublicTypePattern.Matches(content);

                if (matches.Count > 1)
                {
                    var relativePath = Path.GetRelativePath(solutionRoot, file);
                    var typeNames = string.Join(", ", matches.Select(m => m.Groups[1].Value));
                    violations.Add($"  {relativePath}: {matches.Count} public types ({typeNames})");
                }
            }
        }

        violations.Should().BeEmpty(
            $"Each source file must declare at most one public type. " +
            $"Violations ({violations.Count}):\n{string.Join("\n", violations)}");
    }

    private static string FindSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.slnx").Length > 0)
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find solution root (no .slnx file found)");
    }
}
```

- [ ] **Step 6b: Remove the 4 tests + 3 helpers from `ArchitectureTests.cs`**

Delete the 4 test methods and the helper declarations: `AllowedArrayReturns`, `PublicTypePattern`, `FindSolutionRoot()`.

- [ ] **Step 6c: Build + test**

```bash
dotnet build && dotnet test tests/GlobalRealEstate.Tests.Architecture/ --no-build --verbosity minimal
```

Expected: `Passed: 20`.

- [ ] **Step 6d: Format + commit**

```bash
dotnet format
git add tests/GlobalRealEstate.Tests.Architecture/
git commit -m "$(cat <<'EOF'
refactor: extract CodeStructureTests from ArchitectureTests

Four file/method shape tests plus AllowedArrayReturns, PublicTypePattern,
and FindSolutionRoot() moved into their own fixture.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Extract `DiRegistrationTests.cs` and delete `ArchitectureTests.cs`

**Files:**
- Create: `tests/GlobalRealEstate.Tests.Architecture/DiRegistrationTests.cs`
- Modify: `tests/GlobalRealEstate.Tests.Architecture/TestHelpers.cs` — flip `typeof(ArchitectureTests)` to `typeof(LayerDependencyTests)`
- Delete: `tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs`

The 1 remaining test:
- `Should_register_every_public_core_interface_in_add_core_services`

- [ ] **Step 7a: Create `DiRegistrationTests.cs`**

Write to `tests/GlobalRealEstate.Tests.Architecture/DiRegistrationTests.cs`:

```csharp
using FluentAssertions;
using GlobalRealEstate.Core;
using Microsoft.Extensions.DependencyInjection;

namespace GlobalRealEstate.Tests.Architecture;

[TestFixture]
public class DiRegistrationTests
{
    [Test]
    public void Should_register_every_public_core_interface_in_add_core_services()
    {
        var services = new ServiceCollection();
        services.AddCoreServices();
        var provider = services.BuildServiceProvider();

        var coreInterfaces = TestHelpers.CoreAssembly.GetTypes()
            .Where(t => t.IsInterface
                && t.IsPublic
                && t.Namespace?.StartsWith("GlobalRealEstate.Core", StringComparison.Ordinal) == true)
            .ToList();

        var missing = coreInterfaces
            .Where(iface => provider.GetService(iface) == null)
            .Select(iface => $"  {iface.FullName}")
            .ToList();

        missing.Should().BeEmpty(
            $"Every public Core interface must be registered in AddCoreServices(). " +
            $"Missing ({missing.Count}):\n{string.Join("\n", missing)}");
    }
}
```

- [ ] **Step 7b: Update `TestHelpers.cs` to reference `LayerDependencyTests`**

In `TestHelpers.cs`, change:
```csharp
        typeof(ArchitectureTests).Assembly,
```
to:
```csharp
        typeof(LayerDependencyTests).Assembly,
```

(Any of the new fixture types would work — they're all in the same assembly. `LayerDependencyTests` chosen as a stable, alphabetically-first reference.)

- [ ] **Step 7c: Delete `ArchitectureTests.cs`**

Run:
```bash
git rm tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs
```

(If `ArchitectureTests.cs` still has anything other than the empty class declaration after Tasks 3-6, **stop** and figure out what was missed.)

- [ ] **Step 7d: Build + test**

```bash
dotnet build && dotnet test tests/GlobalRealEstate.Tests.Architecture/ --no-build --verbosity minimal
```

Expected: `Passed: 20`.

- [ ] **Step 7e: Format + commit**

```bash
dotnet format
git add tests/GlobalRealEstate.Tests.Architecture/
git commit -m "$(cat <<'EOF'
refactor: extract DiRegistrationTests; delete now-empty ArchitectureTests

The DI registration test (the load-bearing rule from CLAUDE.md) gets its
own fixture. ArchitectureTests.cs is now empty and removed; TestHelpers
now references LayerDependencyTests.Assembly for its TestAssemblies array.

Closes the architecture-test split: 5 fixtures + TestHelpers, 20 tests
distributed by purpose.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

Expected: pre-commit passes, 20 architecture tests still green.

---

## Task 8: Verify, push, open PR

**Files:** none (verification + GitHub)

- [ ] **Step 8a: Final test counts**

```bash
dotnet test --no-build --verbosity minimal 2>&1 | grep -E "Passed!|Failed!"
```

Expected:
- `GlobalRealEstate.Tests.Architecture`: **Passed: 20** (unchanged from baseline)
- `GlobalRealEstate.Tests.Analyzers`: **Passed: 70** (unchanged)
- `GlobalRealEstate.Tests.Unit`: **Passed: 1** (unchanged)
- `GlobalRealEstate.Tests.Integration`: **Passed: 1** (unchanged)

- [ ] **Step 8b: Verify file inventory**

```bash
ls tests/GlobalRealEstate.Tests.Architecture/*.cs
```

Expected exactly:
- `CodeStructureTests.cs`
- `DiRegistrationTests.cs`
- `LayerDependencyTests.cs`
- `NamingConventionTests.cs`
- `ServiceShapeTests.cs`
- `TestHelpers.cs`

(NO `ArchitectureTests.cs`.)

- [ ] **Step 8c: Push and open PR**

```bash
git push -u origin HEAD
```

Then (substitute `<N>` for the M2 issue number):

```bash
gh pr create --base main --title "Split ArchitectureTests.cs into focused fixture files" --body "$(cat <<'EOF'
Closes #<N>. Part of #13 (template extraction).

## Changes

Mechanical refactor — no behavior change. The 636-line ArchitectureTests.cs split into:

- \`TestHelpers.cs\` — shared assembly accessors, ServiceNamespaces, IsRecord/IsDbContext
- \`LayerDependencyTests.cs\` (4 tests) — layer-boundary rules
- \`NamingConventionTests.cs\` (8 tests) — naming + suffix conventions
- \`ServiceShapeTests.cs\` (3 tests) — DI shape
- \`CodeStructureTests.cs\` (4 tests) — file/method structure
- \`DiRegistrationTests.cs\` (1 test) — every Core interface registered

Also drops a dead \`Core.Scraping\` reference in the layer-deps interface test (never matched anything).

## Test plan

- [x] Architecture tests: 20 passed (unchanged from baseline)
- [x] Analyzer tests: 70 passed (unchanged)
- [x] Unit + Integration: 1 each (unchanged)
- [x] Pre-commit hook passes on every commit on this branch

Plan: \`docs/superpowers/plans/2026-04-25-m2-split-architecture-tests.md\`
Spec: \`docs/superpowers/specs/2026-04-25-etl-api-template-extraction-design.md\`
EOF
)"
```

Expected: PR URL printed.

---

## Done

After Step 8c, M2 is complete. Reviewer eyeballs and merges. Next milestone is M3 (template machinery — restructure into `content/` + `.template.config/`).
