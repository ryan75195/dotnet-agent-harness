using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using GlobalRealEstate.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetArchTest.Rules;

namespace GlobalRealEstate.Tests.Architecture;

[TestFixture]
public class ArchitectureTests
{
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

    [Test]
    public void Should_keep_models_free_of_data_layer_dependencies()
    {
        Types.InAssembly(CoreAssembly)
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
        Types.InAssembly(CoreAssembly)
            .That().ResideInNamespaceContaining("Core.Interfaces")
            .ShouldNot().HaveDependencyOnAny(
                "GlobalRealEstate.Core.Data",
                "GlobalRealEstate.Core.Scraping",
                "Microsoft.EntityFrameworkCore")
            .GetResult().IsSuccessful.Should().BeTrue(
                "Interfaces must not depend on concrete data or scraping implementations");
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
        Types.InAssembly(EtlAssembly)
            .That().ResideInNamespaceContaining("Etl")
            .ShouldNot().HaveDependencyOnAny("GlobalRealEstate.Api")
            .GetResult().IsSuccessful.Should().BeTrue(
                "ETL pipeline must not reference the API layer");
    }

    [Test]
    public void Should_require_interfaces_on_core_service_classes()
    {
        var serviceTypes = CoreAssembly.GetTypes()
            .Where(t => t.IsClass
                && t.IsPublic
                && !t.IsAbstract
                && ServiceNamespaces.Contains(t.Namespace)
                && !IsRecord(t)
                && !IsDbContext(t))
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
    public void Should_require_record_types_in_core_models()
    {
        var modelTypes = CoreAssembly.GetTypes()
            .Where(t => t.IsPublic
                && t.Namespace == "GlobalRealEstate.Core.Models")
            .ToList();

        Assert.Multiple(() =>
        {
            foreach (var type in modelTypes)
            {
                IsRecord(type).Should().BeTrue(
                    $"{type.Name} in Core.Models must be a record for immutability");
            }
        });
    }

    [Test]
    public void Should_match_interface_naming_suffix_on_implementations()
    {
        var serviceTypes = CoreAssembly.GetTypes()
            .Where(t => t.IsClass
                && t.IsPublic
                && !t.IsAbstract
                && ServiceNamespaces.Contains(t.Namespace)
                && !IsRecord(t)
                && !IsDbContext(t))
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
        var entityTypes = CoreAssembly.GetTypes()
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
        var testFixtures = TestAssemblies
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

    private static readonly Regex TestMethodPattern = new(
        @"^Should(_[a-z0-9]+)+$", RegexOptions.Compiled);

    private static readonly Regex PublicTypePattern = new(
        @"^\s*public\s+(?:(?:static|sealed|abstract|partial|readonly)\s+)*(?:record\s+(?:struct|class)\s+|(?:class|record|struct|enum|interface)\s+)(\w+)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    [Test]
    public void Should_follow_naming_convention_for_all_test_methods()
    {
        var testAttribute = typeof(TestAttribute);
        var testCaseAttribute = typeof(TestCaseAttribute);

        var testMethods = TestAssemblies
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

    private static readonly HashSet<string> FrameworkAsyncMethods =
    [
        "DisposeAsync",
        "ExecuteAsync",
        "StartAsync",
        "StopAsync"
    ];

    [Test]
    public void Should_not_use_async_suffix_on_method_names()
    {
        var assemblies = new[] { CoreAssembly, EtlAssembly, ApiAssembly };

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

    private static readonly HashSet<string> AllowedSuffixes =
    [
        "Service", "Repository", "Client", "Store", "Context",
        "Entity", "Command", "Parser", "Converter", "Pool",
        "Worker", "Process", "Extensions", "Mapper", "Extractor",
        "Probe", "Result", "Monitor", "Plugin", "Filter"
    ];

    [Test]
    public void Should_use_recognised_role_suffix_on_concrete_classes()
    {
        var assemblies = new[] { CoreAssembly, EtlAssembly, ApiAssembly };

        var concreteClasses = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass
                && t.IsPublic
                && !t.IsAbstract
                && !IsRecord(t)
                && !IsDbContext(t)
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
        var assemblies = new[] { CoreAssembly, EtlAssembly, ApiAssembly };

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

    [Test]
    public void Should_have_test_fixture_for_every_public_class()
    {
        var assemblies = new[] { CoreAssembly, EtlAssembly, ApiAssembly };

        var testFixtureNames = TestAssemblies
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
                && !IsRecord(t)
                && !IsDbContext(t)
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

        var sourceAssemblies = new[] { CoreAssembly, EtlAssembly, ApiAssembly };

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

    private static readonly HashSet<string> AllowedArrayReturns = [];

    [Test]
    public void Should_not_return_arrays_from_public_methods()
    {
        var assemblies = new[] { CoreAssembly, EtlAssembly, ApiAssembly };

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

    private static readonly HashSet<Type> AllowedConcreteParams =
    [
        typeof(string),
        typeof(System.IO.StreamReader),
        typeof(System.IO.StreamWriter),
        typeof(HttpClient),
        typeof(TimeProvider),
    ];

    [Test]
    public void Should_inject_dependencies_as_interfaces_not_concrete_types()
    {
        var assemblies = new[] { CoreAssembly, EtlAssembly, ApiAssembly };

        var serviceClasses = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass
                && t.IsPublic
                && !t.IsAbstract
                && !IsRecord(t)
                && !IsDbContext(t)
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

                    if (IsRecord(paramType))
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
        var commandTypes = EtlAssembly.GetTypes()
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

    [Test]
    public void Should_register_every_public_core_interface_in_add_core_services()
    {
        var services = new ServiceCollection();
        services.AddCoreServices();
        var provider = services.BuildServiceProvider();

        var coreInterfaces = CoreAssembly.GetTypes()
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
