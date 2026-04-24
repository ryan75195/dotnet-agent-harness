using GlobalRealEstate.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace GlobalRealEstate.Tests.Analyzers;

[TestFixture]
public class ConstructorParameterCountAnalyzerTests
{
    [Test]
    public async Task Should_report_when_constructor_exceeds_parameter_limit()
    {
        var source = """
            public class BigService
            {
                public BigService(int a, int b, int c, int d, int e, int f) { }
            }
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0005")
            .WithSpan(3, 5, 3, 68)
            .WithArguments("BigService", 6, 5);

        var test = new CSharpAnalyzerTest<ConstructorParameterCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_when_constructor_is_within_limit()
    {
        var source = """
            public class OkService
            {
                public OkService(int a, int b, int c, int d, int e) { }
            }
            """;

        var test = new CSharpAnalyzerTest<ConstructorParameterCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_records_with_primary_constructor()
    {
        var source = """
            namespace System.Runtime.CompilerServices
            {
                public static class IsExternalInit { }
            }

            public record BigRecord(int A, int B, int C, int D, int E, int F);
            """;

        var test = new CSharpAnalyzerTest<ConstructorParameterCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_record_with_explicit_constructor()
    {
        var source = """
            public record BigRecord
            {
                public BigRecord(int a, int b, int c, int d, int e, int f) { }
            }
            """;

        var test = new CSharpAnalyzerTest<ConstructorParameterCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_test_fixtures()
    {
        var source = """
            namespace NUnit.Framework
            {
                public class TestFixtureAttribute : System.Attribute { }
            }

            namespace GlobalRealEstate.Tests.Unit
            {
                [NUnit.Framework.TestFixture]
                public class BigTests
                {
                    public BigTests(int a, int b, int c, int d, int e, int f) { }
                }
            }
            """;

        var test = new CSharpAnalyzerTest<ConstructorParameterCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_test_namespace_without_attribute()
    {
        var source = """
            namespace MyApp.Tests.Unit
            {
                public class BigHelper
                {
                    public BigHelper(int a, int b, int c, int d, int e, int f) { }
                }
            }
            """;

        var test = new CSharpAnalyzerTest<ConstructorParameterCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_static_constructor()
    {
        var source = """
            public class MyService
            {
                static MyService() { }
            }
            """;

        var test = new CSharpAnalyzerTest<ConstructorParameterCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }
}
