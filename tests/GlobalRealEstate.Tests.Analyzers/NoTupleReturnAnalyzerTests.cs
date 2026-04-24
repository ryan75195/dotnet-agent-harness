using GlobalRealEstate.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace GlobalRealEstate.Tests.Analyzers;

[TestFixture]
public class NoTupleReturnAnalyzerTests
{
    [Test]
    public async Task Should_report_diagnostic_for_tuple_return()
    {
        // Mirrors SitemapCrawler's pattern: a method returning a tuple
        var source = """
            using System.Collections.Generic;

            public class CrawlResult
            {
                private readonly List<(string Url, string Error)> _errors = new();
                public IEnumerable<(string Url, string Error)> Errors => _errors;
            }
            """;

        var expected = new[]
        {
            DiagnosticResult.CompilerWarning("CI0001")
                .WithSpan(6, 5, 6, 70)
                .WithArguments("Errors"),
        };

        var test = new CSharpAnalyzerTest<NoTupleReturnAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected[0] },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_diagnostic_for_non_tuple_return()
    {
        var source = """
            public class MyService
            {
                public string GetName() => "hello";
                public int Count { get; }
            }
            """;

        var test = new CSharpAnalyzerTest<NoTupleReturnAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_diagnostic_for_task_of_tuple_return()
    {
        var source = """
            using System.Threading.Tasks;

            public class MyService
            {
                public Task<(int Id, string Name)> GetItemAsync() => throw new System.NotImplementedException();
            }
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0001")
            .WithSpan(5, 5, 5, 101)
            .WithArguments("GetItemAsync");

        var test = new CSharpAnalyzerTest<NoTupleReturnAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }
}
