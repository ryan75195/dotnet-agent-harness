using GlobalRealEstate.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace GlobalRealEstate.Tests.Analyzers;

[TestFixture]
public class NoCommentsAnalyzerTests
{
    [Test]
    public async Task Should_report_diagnostic_for_single_line_comment()
    {
        var source = """
            public class Foo
            {
                public int Bar() => 1; // trailing note
            }
            """;

        var expected = DiagnosticResult.CompilerError("CI0013").WithSpan(3, 28, 3, 44);

        var test = new CSharpAnalyzerTest<NoCommentsAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_diagnostic_for_multi_line_comment()
    {
        var source = """
            public class Foo
            {
                /* block note */
                public int Bar() => 1;
            }
            """;

        var expected = DiagnosticResult.CompilerError("CI0013").WithSpan(3, 5, 3, 21);

        var test = new CSharpAnalyzerTest<NoCommentsAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_diagnostic_for_xml_doc_comment()
    {
        var source = """
            public class Foo
            {
                /// <summary>Gets a value.</summary>
                public int Bar() => 1;
            }
            """;

        var expected = DiagnosticResult.CompilerError("CI0013").WithSpan(3, 8, 4, 1);

        var test = new CSharpAnalyzerTest<NoCommentsAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_diagnostic_for_comment_free_code()
    {
        var source = """
            public class Foo
            {
                public int Bar() => 1;
            }
            """;

        var test = new CSharpAnalyzerTest<NoCommentsAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_diagnostic_for_comment_like_text_inside_string_literal()
    {
        var source = """
            public class Foo
            {
                public string Url => "http://example.com/path";
            }
            """;

        var test = new CSharpAnalyzerTest<NoCommentsAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }
}
