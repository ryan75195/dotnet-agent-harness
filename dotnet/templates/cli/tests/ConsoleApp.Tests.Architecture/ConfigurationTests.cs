using System.Reflection;
using System.Text.Json;
using FluentAssertions;

namespace ConsoleApp.Tests.Architecture;

[TestFixture]
public class ConfigurationTests
{
    private const string OptOutMetadataKey = "HarnessOptionsDefaultsInCode";

    private static readonly HashSet<string> FrameworkSections =
        new(["Logging", "Serilog", "AllowedHosts"], StringComparer.OrdinalIgnoreCase);

    [Test]
    public void Should_map_every_appsettings_key_to_an_options_property()
    {
        using var settings = LoadAppSettings();
        var sections = OptionsSections();
        var violations = new List<string>();

        WalkSections(settings.RootElement, string.Empty, sections, violations);

        violations.Should().BeEmpty(
            "every key in appsettings.json must bind to a property of an options class " +
            "(a class ending in Options with a const SectionName); a key nothing reads is a silent typo. " +
            $"Unbound keys ({violations.Count}):\n{string.Join("\n", violations)}");
    }

    [Test]
    public void Should_give_every_options_property_a_value_in_appsettings()
    {
        using var settings = LoadAppSettings();
        var violations = new List<string>();

        foreach (var (sectionName, type) in OptionsSections())
        {
            if (DefaultsLiveInCode(type.Assembly))
            {
                continue;
            }

            var section = FindSection(settings.RootElement, sectionName);
            violations.AddRange(BindableProperties(type)
                .Where(p => section is null || !HasKey(section.Value, p.Name))
                .Select(p => $"  {sectionName}:{p.Name} ({type.Name}.{p.Name})"));
        }

        violations.Should().BeEmpty(
            "option values live in appsettings.json, not in code (CI0021); give every property a key, " +
            "using null for a value that must come from the environment or user secrets. " +
            $"Missing keys ({violations.Count}):\n{string.Join("\n", violations)}");
    }

    [Test]
    public void Should_keep_the_cli_option_defaults_in_appsettings()
    {
        DefaultsLiveInCode(TestHelpers.CliAssembly).Should().BeFalse(
            "only a packaged SDK library may set HarnessOptionsDefaultsInCode; the app's defaults belong in its appsettings.json");
    }

    private static JsonDocument LoadAppSettings()
    {
        var cliProject = TestHelpers.CliAssembly.GetName().Name!;
        var path = Path.Combine(SolutionRoot(), "src", cliProject, "appsettings.json");
        File.Exists(path).Should().BeTrue($"the CLI ships its configuration in {path}");
        return JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
    }

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && directory.GetFiles("*.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run from inside the solution tree");
        return directory!.FullName;
    }

    private static Dictionary<string, Type> OptionsSections()
    {
        var sections = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in new[] { TestHelpers.CoreAssembly, TestHelpers.CliAssembly }.SelectMany(a => a.GetTypes()))
        {
            var sectionName = SectionNameOf(type);
            if (sectionName is not null)
            {
                sections[sectionName] = type;
            }
        }

        return sections;
    }

    private static string? SectionNameOf(Type type)
    {
        if (!type.IsClass || !type.Name.EndsWith("Options", StringComparison.Ordinal))
        {
            return null;
        }

        var field = type.GetField("SectionName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        return field is { IsLiteral: true } ? field.GetRawConstantValue() as string : null;
    }

    private static bool DefaultsLiveInCode(Assembly assembly) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Any(a => a.Key == OptOutMetadataKey && string.Equals(a.Value, "true", StringComparison.OrdinalIgnoreCase));

    private static void WalkSections(JsonElement element, string path, Dictionary<string, Type> sections, List<string> violations)
    {
        foreach (var child in element.EnumerateObject())
        {
            var childPath = path.Length == 0 ? child.Name : $"{path}:{child.Name}";
            if (path.Length == 0 && FrameworkSections.Contains(child.Name))
            {
                continue;
            }

            if (sections.TryGetValue(childPath, out var type))
            {
                CheckAgainstType(child.Value, type, childPath, sections, violations);
            }
            else if (child.Value.ValueKind == JsonValueKind.Object && IsSectionParent(childPath, sections))
            {
                WalkSections(child.Value, childPath, sections, violations);
            }
            else
            {
                violations.Add($"  {childPath}");
            }
        }
    }

    private static void CheckAgainstType(JsonElement element, Type type, string path, Dictionary<string, Type> sections, List<string> violations)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            violations.Add($"  {path} (expected an object for {type.Name})");
            return;
        }

        var properties = BindableProperties(type).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var child in element.EnumerateObject())
        {
            var childPath = $"{path}:{child.Name}";
            if (properties.TryGetValue(child.Name, out var property))
            {
                if (child.Value.ValueKind == JsonValueKind.Object && IsNestedOptions(property.PropertyType))
                {
                    CheckAgainstType(child.Value, property.PropertyType, childPath, sections, violations);
                }
            }
            else if (sections.TryGetValue(childPath, out var nested))
            {
                CheckAgainstType(child.Value, nested, childPath, sections, violations);
            }
            else if (!(child.Value.ValueKind == JsonValueKind.Object && IsSectionParent(childPath, sections)))
            {
                violations.Add($"  {childPath} (no property {child.Name} on {type.Name})");
            }
        }
    }

    private static bool IsSectionParent(string path, Dictionary<string, Type> sections) =>
        sections.Keys.Any(k => k.StartsWith(path + ":", StringComparison.OrdinalIgnoreCase));

    private static bool IsNestedOptions(Type type) =>
        type.IsClass && type != typeof(string) && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type);

    private static IEnumerable<PropertyInfo> BindableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && p.GetIndexParameters().Length == 0);

    private static JsonElement? FindSection(JsonElement root, string sectionName)
    {
        var current = root;
        foreach (var part in sectionName.Split(':'))
        {
            var match = current.ValueKind == JsonValueKind.Object
                ? current.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, part, StringComparison.OrdinalIgnoreCase))
                : default;
            if (match.Value.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            current = match.Value;
        }

        return current;
    }

    private static bool HasKey(JsonElement section, string name) =>
        section.ValueKind == JsonValueKind.Object
        && section.EnumerateObject().Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}
