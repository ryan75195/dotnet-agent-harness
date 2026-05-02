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
                }
            }
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0002")
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
    public async Task Should_not_report_when_all_methods_tested()
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
