using ConsoleApp.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace ConsoleApp.Tests.Analyzers;

[TestFixture]
public class TestFixtureSourceTypeAnalyzerCriterion3Tests
{
    [Test]
    public async Task Should_not_report_non_unit_fixture()
    {
        var source = """
            namespace Other.Framework { public class IntegrationFixtureAttribute : System.Attribute { } }
            [Other.Framework.IntegrationFixture]
            public class EndpointScenario { }
            """;

        var test = new CSharpAnalyzerTest<TestFixtureSourceTypeAnalyzer, DefaultVerifier>
        {
            TestCode = source
        };

        await test.RunAsync();
    }
}
