using EtlApi.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace EtlApi.Tests.Analyzers;

[TestFixture]
public class TestCoverageAnalyzerTests
{
    private const string NUnitStubs = """
        namespace NUnit.Framework
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class TestFixtureAttribute : System.Attribute { }
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class TestAttribute : System.Attribute { }
        }
        namespace NUnit.Framework
        {
            public static class Assert
            {
                public static void That(object actual, object constraint) { }
                public static void Throws<T>(System.Action code) where T : System.Exception { }
                public static void Multiple(System.Action action) { }
            }
            public static class Is
            {
                public static object EqualTo(object expected) => null;
                public static object Not => null;
            }
        }
        namespace FluentAssertions
        {
            public static class AssertionExtensions
            {
                public static object Should(this object actual) => null;
            }
        }
        """;

    [Test]
    public async Task Should_report_uncovered_public_method()
    {
        var source = """
            public class FooService
            {
                public void DoSomething() { }
                public int Calculate() => 42;
            }

            [NUnit.Framework.TestFixture]
            public class FooServiceTests
            {
                [NUnit.Framework.Test]
                public void Should_do_something()
                {
                    var sut = new FooService();
                    sut.DoSomething();
                    NUnit.Framework.Assert.That(true, NUnit.Framework.Is.EqualTo(true));
                }
            }
            """;

        var expected = DiagnosticResult.CompilerError("CI0002")
            .WithSpan(8, 14, 8, 29)
            .WithArguments("FooServiceTests", "FooService.Calculate");

        var test = new CSharpAnalyzerTest<TestCoverageAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_when_method_invoked_without_assertion()
    {
        var source = """
            public class FooService
            {
                public void DoSomething() { }
                public int Calculate() => 42;
            }

            [NUnit.Framework.TestFixture]
            public class FooServiceTests
            {
                [NUnit.Framework.Test]
                public void Should_do_something()
                {
                    var sut = new FooService();
                    sut.DoSomething();
                }

                [NUnit.Framework.Test]
                public void Should_calculate()
                {
                    var sut = new FooService();
                    var result = sut.Calculate();
                }
            }
            """;

        var expectedDoSomething = DiagnosticResult.CompilerError("CI0002")
            .WithSpan(8, 14, 8, 29)
            .WithArguments("FooServiceTests", "FooService.DoSomething");

        var expectedCalculate = DiagnosticResult.CompilerError("CI0002")
            .WithSpan(8, 14, 8, 29)
            .WithArguments("FooServiceTests", "FooService.Calculate");

        var test = new CSharpAnalyzerTest<TestCoverageAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
            ExpectedDiagnostics = { expectedDoSomething, expectedCalculate },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_when_all_methods_tested_with_assertions()
    {
        var source = """
            public class FooService
            {
                public void DoSomething() { }
                public int Calculate() => 42;
            }

            [NUnit.Framework.TestFixture]
            public class FooServiceTests
            {
                [NUnit.Framework.Test]
                public void Should_do_something()
                {
                    var sut = new FooService();
                    sut.DoSomething();
                    NUnit.Framework.Assert.That(true, NUnit.Framework.Is.EqualTo(true));
                }

                [NUnit.Framework.Test]
                public void Should_calculate()
                {
                    var sut = new FooService();
                    var result = sut.Calculate();
                    NUnit.Framework.Assert.That(result, NUnit.Framework.Is.EqualTo(42));
                }
            }
            """;

        var test = new CSharpAnalyzerTest<TestCoverageAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_accept_fluent_assertions()
    {
        var source = """
            using FluentAssertions;

            public class FooService
            {
                public int Calculate() => 42;
            }

            [NUnit.Framework.TestFixture]
            public class FooServiceTests
            {
                [NUnit.Framework.Test]
                public void Should_calculate()
                {
                    var sut = new FooService();
                    var result = sut.Calculate();
                    result.Should();
                }
            }
            """;

        var test = new CSharpAnalyzerTest<TestCoverageAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_accept_assert_throws()
    {
        var source = """
            public class FooService
            {
                public void DoSomething() { throw new System.InvalidOperationException(); }
            }

            [NUnit.Framework.TestFixture]
            public class FooServiceTests
            {
                [NUnit.Framework.Test]
                public void Should_throw_when_called()
                {
                    var sut = new FooService();
                    NUnit.Framework.Assert.Throws<System.InvalidOperationException>(() => sut.DoSomething());
                }
            }
            """;

        var test = new CSharpAnalyzerTest<TestCoverageAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_dispose_methods()
    {
        var source = """
            public class FooService : System.IDisposable
            {
                public void DoWork() { }
                public void Dispose() { }
            }

            [NUnit.Framework.TestFixture]
            public class FooServiceTests
            {
                [NUnit.Framework.Test]
                public void Should_do_work()
                {
                    var sut = new FooService();
                    sut.DoWork();
                    NUnit.Framework.Assert.That(true, NUnit.Framework.Is.EqualTo(true));
                }
            }
            """;

        var test = new CSharpAnalyzerTest<TestCoverageAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_property_accessors()
    {
        var source = """
            public class FooService
            {
                public string Name { get; set; }
                public void DoWork() { }
            }

            [NUnit.Framework.TestFixture]
            public class FooServiceTests
            {
                [NUnit.Framework.Test]
                public void Should_do_work()
                {
                    var sut = new FooService();
                    sut.DoWork();
                    NUnit.Framework.Assert.That(true, NUnit.Framework.Is.EqualTo(true));
                }
            }
            """;

        var test = new CSharpAnalyzerTest<TestCoverageAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_object_overrides()
    {
        var source = """
            public class FooService
            {
                public void DoWork() { }
                public override string ToString() => "foo";
                public override bool Equals(object obj) => false;
                public override int GetHashCode() => 0;
            }

            [NUnit.Framework.TestFixture]
            public class FooServiceTests
            {
                [NUnit.Framework.Test]
                public void Should_do_work()
                {
                    var sut = new FooService();
                    sut.DoWork();
                    NUnit.Framework.Assert.That(true, NUnit.Framework.Is.EqualTo(true));
                }
            }
            """;

        var test = new CSharpAnalyzerTest<TestCoverageAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_when_no_matching_source_type()
    {
        var source = """
            [NUnit.Framework.TestFixture]
            public class OrphanTests
            {
                [NUnit.Framework.Test]
                public void Should_pass() { }
            }
            """;

        var test = new CSharpAnalyzerTest<TestCoverageAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_detect_calls_through_interface()
    {
        var source = """
            public interface IFooService
            {
                void DoWork();
                int Calculate();
            }

            public class FooService : IFooService
            {
                public void DoWork() { }
                public int Calculate() => 42;
            }

            [NUnit.Framework.TestFixture]
            public class FooServiceTests
            {
                [NUnit.Framework.Test]
                public void Should_do_work()
                {
                    IFooService sut = new FooService();
                    sut.DoWork();
                    sut.Calculate();
                    NUnit.Framework.Assert.That(true, NUnit.Framework.Is.EqualTo(true));
                }
            }
            """;

        var test = new CSharpAnalyzerTest<TestCoverageAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, NUnitStubs } },
        };

        await test.RunAsync();
    }
}
