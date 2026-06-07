using ConsoleApp.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace ConsoleApp.Tests.Analyzers;

[TestFixture]
public class NoAssertIgnoreAnalyzerTests
{
    [Test]
    public async Task Should_report_assert_ignore()
    {
        var source = """
            using NUnit.Framework;
            [TestFixture]
            public class MyTests
            {
                [Test]
                public void Should_do_something()
                {
                    {|#0:Assert.Ignore("not configured")|};
                }
            }
            """;

        var expected = new DiagnosticResult(NoAssertIgnoreAnalyzer.DiagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithLocation(0);

        var test = new CSharpAnalyzerTest<NoAssertIgnoreAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        test.TestState.AdditionalReferences.Add(typeof(TestFixtureAttribute).Assembly);
        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_assert_that()
    {
        var source = """
            using NUnit.Framework;
            [TestFixture]
            public class MyTests
            {
                [Test]
                public void Should_do_something()
                {
                    Assert.That(1, Is.EqualTo(1));
                }
            }
            """;

        var test = new CSharpAnalyzerTest<NoAssertIgnoreAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        test.TestState.AdditionalReferences.Add(typeof(TestFixtureAttribute).Assembly);
        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_non_nunit_ignore()
    {
        var source = """
            public static class Assert
            {
                public static void Ignore(string msg) { }
            }
            public class MyTests
            {
                public void Should_do_something()
                {
                    Assert.Ignore("this is fine");
                }
            }
            """;

        var test = new CSharpAnalyzerTest<NoAssertIgnoreAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        await test.RunAsync();
    }
}
