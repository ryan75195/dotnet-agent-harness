using ConsoleApp.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace ConsoleApp.Tests.Analyzers;

[TestFixture]
public class TestFixtureSourceTypeAnalyzerCriterion1Tests
{
    [Test]
    public async Task Should_report_unit_fixture_without_solution_type()
    {
        var source = """
            namespace NUnit.Framework { public class TestFixtureAttribute : System.Attribute { } }
            [NUnit.Framework.TestFixture]
            public class MissingSubjectTests { }
            """;

        var test = new CSharpAnalyzerTest<TestFixtureSourceTypeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics =
            {
                DiagnosticResult.CompilerError("CI0019").WithSpan(3, 14, 3, 33)
            }
        };

        await test.RunAsync();
    }
}
