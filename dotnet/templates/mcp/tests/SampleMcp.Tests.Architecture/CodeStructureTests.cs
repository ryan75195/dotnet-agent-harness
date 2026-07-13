using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace SampleMcp.Tests.Architecture;

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
        var assemblies = new[] { TestHelpers.CoreAssembly, TestHelpers.ServerAssembly };

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
                && (!t.IsAbstract || t.IsSealed)
                && !t.Name.EndsWith("Extensions", StringComparison.Ordinal)
                && !TestHelpers.IsRecord(t)
                && !TestHelpers.IsDbContext(t)
                && t.Namespace?.Contains(".Entities", StringComparison.Ordinal) != true
                && t.Name is not "Program" and not "AssemblyMarker")
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
            ["SampleMcp.Core"] = ".Core",
            ["SampleMcp.Server"] = ".Server"
        };

        var sourceAssemblies = new[] { TestHelpers.CoreAssembly, TestHelpers.ServerAssembly };

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
        var assemblies = new[] { TestHelpers.CoreAssembly, TestHelpers.ServerAssembly };

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
            Path.Combine(solutionRoot, "src", "SampleMcp.Core"),
            Path.Combine(solutionRoot, "src", "SampleMcp.Server")
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
    public void Should_have_roundtrip_tests_for_polymorphic_json_types()
    {
        var assemblies = new[] { TestHelpers.CoreAssembly, TestHelpers.ServerAssembly };

        var polymorphicBaseTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsPublic
                && t.GetCustomAttributes<JsonPolymorphicAttribute>().Any())
            .ToList();

        if (polymorphicBaseTypes.Count == 0)
        {
            Assert.Pass("No [JsonPolymorphic] types found — nothing to enforce.");
        }

        var testFixtureNames = TestHelpers.TestAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && t.IsPublic
                && t.Name.EndsWith("Tests", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToHashSet();

        var violations = polymorphicBaseTypes
            .Where(t => !testFixtureNames.Contains(t.Name + "RoundtripTests")
                && !testFixtureNames.Contains(t.Name + "SerializationTests"))
            .Select(t =>
            {
                var derivedCount = t.GetCustomAttributes<JsonDerivedTypeAttribute>().Count();
                return $"  {t.FullName} ({derivedCount} derived types) — expected {t.Name}RoundtripTests";
            })
            .ToList();

        violations.Should().BeEmpty(
            $"Types with [JsonPolymorphic] must have a roundtrip test fixture " +
            $"that serializes and deserializes every [JsonDerivedType]. " +
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
