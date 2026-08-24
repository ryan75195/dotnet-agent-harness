namespace ConsoleApp.Tests.Analyzers;

[TestFixture]
public class TestFixtureSourceTypeAnalyzerCriterion5Tests
{
    [Test]
    public void Should_name_each_template_unit_smoke_fixture_after_an_existing_type()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtures = new Dictionary<string, string>
        {
            ["dotnet/templates/cli/tests/ConsoleApp.Tests.Unit/SmokeTests.cs"] = "AssemblyMarkerTests",
            ["dotnet/templates/etl-api/tests/EtlApi.Tests.Unit/SmokeTests.cs"] = "AssemblyMarkerTests",
            ["dotnet/templates/mcp/tests/SampleMcp.Tests.Unit/SmokeTests.cs"] = "AssemblyMarkerTests",
            ["dotnet/templates/durable/tests/SampleDurable.Tests.Unit/SmokeTests.cs"] = "AssemblyMarkerTests"
        };

        foreach (var fixture in fixtures)
        {
            var source = File.ReadAllText(Path.Combine(repoRoot, fixture.Key));
            Assert.That(source, Does.Contain($"class {fixture.Value}"), fixture.Key);
            Assert.That(source, Does.Not.Contain("class SmokeTests"), fixture.Key);
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
}
