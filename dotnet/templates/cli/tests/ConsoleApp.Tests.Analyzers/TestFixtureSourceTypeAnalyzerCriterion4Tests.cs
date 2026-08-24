using ConsoleApp.Analyzers;

namespace ConsoleApp.Tests.Analyzers;

[TestFixture]
public class TestFixtureSourceTypeAnalyzerCriterion4Tests
{
    [Test]
    public void Should_use_the_next_unused_diagnostic_identifier()
    {
        var identifiersAlreadyInUse = new[]
        {
            "CI0001", "CI0002", "CI0003", "CI0004", "CI0005", "CI0006",
            "CI0007", "CI0008", "CI0009", "CI0010", "CI0011", "CI0012",
            "CI0013", "CI0014", "CI0015", "CI0016", "CI0017", "CI0018"
        };

        Assert.That(identifiersAlreadyInUse, Does.Not.Contain(TestFixtureSourceTypeAnalyzer.DiagnosticId));
        Assert.That(TestFixtureSourceTypeAnalyzer.DiagnosticId, Is.EqualTo("CI0019"));
    }
}
