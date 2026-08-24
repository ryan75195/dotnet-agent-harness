namespace ConsoleApp.Tests.Analyzers;

[TestFixture]
public class TestFixtureSourceTypeAnalyzerCriterion6Tests
{
    [Test]
    public void Should_keep_the_shared_analyzer_suite_free_of_drift()
    {
        var repoRoot = FindRepositoryRoot();
        var templates = new Dictionary<string, string>
        {
            ["cli"] = "ConsoleApp",
            ["etl-api"] = "EtlApi",
            ["mcp"] = "SampleMcp",
            ["durable"] = "SampleDurable"
        };

        var referenceNames = GetReferenceNames(repoRoot);
        var reference = ReadSuite(repoRoot, "cli", "ConsoleApp", referenceNames);
        foreach (var template in templates.Where(template => template.Key != "cli"))
        {
            Assert.That(ReadSuite(repoRoot, template.Key, template.Value, referenceNames), Is.EqualTo(reference), template.Key);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "dotnet", "template-tests", "analyzer-drift.ps1")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found");
    }

    private static string[] GetReferenceNames(string root)
    {
        var directories = new[]
        {
            Path.Combine(root, "dotnet", "templates", "cli", "src", "ConsoleApp.Analyzers"),
            Path.Combine(root, "dotnet", "templates", "cli", "tests", "ConsoleApp.Tests.Analyzers")
        };

        return directories.SelectMany(directory => Directory.GetFiles(directory, "*.cs"))
            .Select(Path.GetFileName)
            .Where(name => name != null)
            .Where(name => !name!.StartsWith("TestFixtureSourceTypeAnalyzerCriterion", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;
    }

    private static string ReadSuite(string root, string template, string namespaceName, IEnumerable<string> names)
    {
        var analyzerDirectory = Path.Combine(root, "dotnet", "templates", template, "src", $"{namespaceName}.Analyzers");
        var testDirectory = Path.Combine(root, "dotnet", "templates", template, "tests", $"{namespaceName}.Tests.Analyzers");
        var files = names.Select(name => Directory.GetFiles(analyzerDirectory, name).FirstOrDefault()
            ?? Directory.GetFiles(testDirectory, name).FirstOrDefault()
            ?? throw new InvalidOperationException($"Shared suite file was not found: {name}"));

        return string.Join("\n", files.Select(path => Path.GetFileName(path) + ":" + File.ReadAllText(path).Replace(namespaceName, "__NS__", StringComparison.Ordinal)));
    }
}
