using EtlApi.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace EtlApi.Tests.Analyzers;

[TestFixture]
public class NotNullOnlyAssertionAnalyzerTests
{
    private const string NUnitStubs = """
        namespace NUnit.Framework
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class TestFixtureAttribute : System.Attribute { }
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class TestAttribute : System.Attribute { }
            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public class TestCaseAttribute : System.Attribute
            {
                public TestCaseAttribute(params object[] args) { }
            }
            public static class Assert
            {
                public static void That(object actual, object constraint) { }
            }
            public static class Is
            {
                public static object Null => null!;
                public static object EqualTo(object expected) => null!;
                public static IsNot Not => new IsNot();
            }
            public class IsNot
            {
                public object Null => null!;
            }
        }
        """;

    [Test]
    public async Task Should_report_when_only_asserting_not_null()
    {
        var source = """
            namespace MyApp.Tests
            {
                [NUnit.Framework.TestFixture]
                public class MyTests
                {
                    [NUnit.Framework.Test]
                    public void Should_test_something()
                    {
                        object result = new object();
                        NUnit.Framework.Assert.That(result, NUnit.Framework.Is.Not.Null);
                    }
                }
            }
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0009")
            .WithSpan(10, 36, 10, 40)
            .WithArguments("Should_test_something", "result");

        var test = new CSharpAnalyzerTest<NotNullOnlyAssertionAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_when_follow_up_assertion_exists()
    {
        var source = """
            namespace MyApp.Tests
            {
                [NUnit.Framework.TestFixture]
                public class MyTests
                {
                    [NUnit.Framework.Test]
                    public void Should_test_something()
                    {
                        object result = new object();
                        NUnit.Framework.Assert.That(result, NUnit.Framework.Is.Not.Null);
                        NUnit.Framework.Assert.That(result.ToString(), NUnit.Framework.Is.EqualTo("hello"));
                    }
                }
            }
            """;

        var test = new CSharpAnalyzerTest<NotNullOnlyAssertionAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_when_no_not_null_assertion()
    {
        var source = """
            namespace MyApp.Tests
            {
                [NUnit.Framework.TestFixture]
                public class MyTests
                {
                    [NUnit.Framework.Test]
                    public void Should_test_something()
                    {
                        object result = new object();
                        NUnit.Framework.Assert.That(result.ToString(), NUnit.Framework.Is.EqualTo("hello"));
                    }
                }
            }
            """;

        var test = new CSharpAnalyzerTest<NotNullOnlyAssertionAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_non_test_methods()
    {
        var source = """
            namespace MyApp.Tests
            {
                [NUnit.Framework.TestFixture]
                public class MyTests
                {
                    public void NotATestMethod()
                    {
                        object result = new object();
                        NUnit.Framework.Assert.That(result, NUnit.Framework.Is.Not.Null);
                    }
                }
            }
            """;

        var test = new CSharpAnalyzerTest<NotNullOnlyAssertionAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_when_using_null_forgiving_member_access()
    {
        var source = """
            namespace MyApp.Tests
            {
                [NUnit.Framework.TestFixture]
                public class MyTests
                {
                    [NUnit.Framework.Test]
                    public void Should_test_something()
                    {
                        object result = new object();
                        NUnit.Framework.Assert.That(result, NUnit.Framework.Is.Not.Null);
                        NUnit.Framework.Assert.That(result!.ToString(), NUnit.Framework.Is.EqualTo("hello"));
                    }
                }
            }
            """;

        var test = new CSharpAnalyzerTest<NotNullOnlyAssertionAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_for_test_case_attribute()
    {
        var source = """
            namespace MyApp.Tests
            {
                [NUnit.Framework.TestFixture]
                public class MyTests
                {
                    [NUnit.Framework.TestCase(42)]
                    public void Should_test_something(int value)
                    {
                        object result = new object();
                        NUnit.Framework.Assert.That(result, NUnit.Framework.Is.Not.Null);
                    }
                }
            }
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0009")
            .WithSpan(10, 36, 10, 40)
            .WithArguments("Should_test_something", "result");

        var test = new CSharpAnalyzerTest<NotNullOnlyAssertionAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }
}
