using GlobalRealEstate.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace GlobalRealEstate.Tests.Analyzers;

[TestFixture]
public class NestedPublicTypeAnalyzerTests
{
    [Test]
    public async Task Should_report_for_public_class_nested_in_class()
    {
        var source = """
            public class Outer
            {
                public class Inner { }
            }
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0006")
            .WithSpan(3, 18, 3, 23)
            .WithArguments("Inner", "Outer");

        var test = new CSharpAnalyzerTest<NestedPublicTypeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_for_public_record_nested_in_class()
    {
        var source = """
            public class Outer
            {
                public record Inner { }
            }
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0006")
            .WithSpan(3, 19, 3, 24)
            .WithArguments("Inner", "Outer");

        var test = new CSharpAnalyzerTest<NestedPublicTypeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_for_public_enum_nested_in_class()
    {
        var source = """
            public class Outer
            {
                public enum Status { Active, Inactive }
            }
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0006")
            .WithSpan(3, 17, 3, 23)
            .WithArguments("Status", "Outer");

        var test = new CSharpAnalyzerTest<NestedPublicTypeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_private_nested_class()
    {
        var source = """
            public class Outer
            {
                private class Inner { }
            }
            """;

        var test = new CSharpAnalyzerTest<NestedPublicTypeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_internal_nested_class()
    {
        var source = """
            public class Outer
            {
                internal class Inner { }
            }
            """;

        var test = new CSharpAnalyzerTest<NestedPublicTypeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_in_test_namespace()
    {
        var source = """
            namespace MyApp.Tests.Unit
            {
                public class Outer
                {
                    public class Inner { }
                }
            }
            """;

        var test = new CSharpAnalyzerTest<NestedPublicTypeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_for_protected_nested_class()
    {
        var source = """
            public class Outer
            {
                protected class Inner { }
            }
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0006")
            .WithSpan(3, 21, 3, 26)
            .WithArguments("Inner", "Outer");

        var test = new CSharpAnalyzerTest<NestedPublicTypeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }
}
