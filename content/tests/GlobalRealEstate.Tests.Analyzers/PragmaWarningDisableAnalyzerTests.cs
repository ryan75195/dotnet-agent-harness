using GlobalRealEstate.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace GlobalRealEstate.Tests.Analyzers;

[TestFixture]
public class PragmaWarningDisableAnalyzerTests
{
    [Test]
    public async Task Should_report_for_pragma_disable_ca_rule()
    {
        var source = """
            #pragma warning disable CA1502
            public class MyService
            {
                public void DoWork() { }
            }
            #pragma warning restore CA1502
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0008")
            .WithSpan(1, 25, 1, 31)
            .WithArguments("CA1502");

        var test = new CSharpAnalyzerTest<PragmaWarningDisableAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_for_pragma_disable_ci_rule()
    {
        var source = """
            #pragma warning disable CI0004
            public class MyService
            {
                public void DoWork() { }
            }
            #pragma warning restore CI0004
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0008")
            .WithSpan(1, 25, 1, 31)
            .WithArguments("CI0004");

        var test = new CSharpAnalyzerTest<PragmaWarningDisableAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_pragma_disable_cs_rule()
    {
        var source = """
            #pragma warning disable CS0168
            public class MyService
            {
                public void DoWork() { }
            }
            #pragma warning restore CS0168
            """;

        var test = new CSharpAnalyzerTest<PragmaWarningDisableAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_pragma_disable_ide_rule()
    {
        var source = """
            #pragma warning disable IDE0051
            public class MyService
            {
                public void DoWork() { }
            }
            #pragma warning restore IDE0051
            """;

        var test = new CSharpAnalyzerTest<PragmaWarningDisableAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_pragma_restore()
    {
        var source = """
            #pragma warning restore CA1502
            public class MyService
            {
                public void DoWork() { }
            }
            """;

        var test = new CSharpAnalyzerTest<PragmaWarningDisableAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }
}
