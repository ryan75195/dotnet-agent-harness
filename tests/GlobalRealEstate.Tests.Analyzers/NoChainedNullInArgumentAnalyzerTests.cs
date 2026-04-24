using GlobalRealEstate.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace GlobalRealEstate.Tests.Analyzers;

[TestFixture]
public class NoChainedNullInArgumentAnalyzerTests
{
    [Test]
    public async Task Should_report_chained_null_ops_in_method_argument()
    {
        var source = """
            public class Foo
            {
                public string Bar(string? input) => Process(input?.Trim() ?? "");
                private string Process(string s) => s;
            }
            """;

        var expected = DiagnosticResult.CompilerError("CI0012")
            .WithSpan(3, 49, 3, 68);

        var test = new CSharpAnalyzerTest<NoChainedNullInArgumentAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_simple_null_coalesce_in_argument()
    {
        var source = """
            public class Foo
            {
                public int Bar(int? limit) => Process(limit ?? 20);
                private int Process(int n) => n;
            }
            """;

        var test = new CSharpAnalyzerTest<NoChainedNullInArgumentAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_chained_null_ops_in_variable_assignment()
    {
        var source = """
            public class Foo
            {
                public string Bar(string? input)
                {
                    var clean = input?.Trim() ?? "";
                    return Process(clean);
                }
                private string Process(string s) => s;
            }
            """;

        var test = new CSharpAnalyzerTest<NoChainedNullInArgumentAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_simple_null_conditional_in_argument()
    {
        var source = """
            public class Foo
            {
                public bool Bar(string? input) => Check(input?.Length);
                private bool Check(int? n) => n > 0;
            }
            """;

        var test = new CSharpAnalyzerTest<NoChainedNullInArgumentAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }
}
