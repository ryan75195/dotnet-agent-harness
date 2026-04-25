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
                var baseInterfaces = type.BaseType?.GetInterfaces() ?? [];
                var projectInterfaces = type.GetInterfaces()
                    .Except(baseInterfaces)
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
