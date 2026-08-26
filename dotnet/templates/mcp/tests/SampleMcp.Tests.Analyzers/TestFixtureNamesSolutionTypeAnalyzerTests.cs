using SampleMcp.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace SampleMcp.Tests.Analyzers;

[TestFixture]
public class TestFixtureNamesSolutionTypeAnalyzerTests
{
    [Test]
    public async Task Should_report_a_fixture_that_names_no_type_in_the_solution()
    {
        await Verify("""
            using NUnit.Framework;
            namespace SampleMcp.Core { public class Widget { } }
            namespace SampleMcp.Tests.Unit
            {
                [TestFixture]
                public class {|#0:GadgetTests|}
                {
                    [Test] public void Should_do_something() { }
                }
            }
            """, Expect("GadgetTests"));
    }

    [Test]
    public async Task Should_report_a_fixture_named_after_a_framework_type()
    {
        await Verify("""
            using NUnit.Framework;
            namespace SampleMcp.Core { public class Widget { } }
            namespace SampleMcp.Tests.Unit
            {
                [TestFixture]
                public class {|#0:StringBuilderTests|}
                {
                    [Test] public void Should_do_something() { }
                }
            }
            """, Expect("StringBuilderTests"));
    }

    [Test]
    public async Task Should_report_a_fixture_that_does_not_end_in_tests()
    {
        await Verify("""
            using NUnit.Framework;
            namespace SampleMcp.Core { public class Widget { } }
            namespace SampleMcp.Tests.Unit
            {
                [TestFixture]
                public class {|#0:WidgetChecks|}
                {
                    [Test] public void Should_do_something() { }
                }
            }
            """, Expect("WidgetChecks"));
    }

    [Test]
    public async Task Should_accept_a_fixture_named_after_a_type_in_the_solution()
    {
        await Verify("""
            using NUnit.Framework;
            namespace SampleMcp.Core { public class Widget { } }
            namespace SampleMcp.Tests.Unit
            {
                [TestFixture]
                public class WidgetTests
                {
                    [Test] public void Should_do_something() { }
                }
            }
            """);
    }

    [Test]
    public async Task Should_leave_a_fixture_outside_the_unit_suite_alone()
    {
        await Verify("""
            using NUnit.Framework;
            namespace SampleMcp.Core { public class Widget { } }
            namespace SampleMcp.Tests.Integration
            {
                [TestFixture]
                public class EndToEndFlowTests
                {
                    [Test] public void Should_do_something() { }
                }
            }
            """);
    }

    [Test]
    public async Task Should_leave_a_class_without_the_fixture_attribute_alone()
    {
        await Verify("""
            using NUnit.Framework;
            namespace SampleMcp.Core { public class Widget { } }
            namespace SampleMcp.Tests.Unit
            {
                public class GadgetHelper
                {
                }
            }
            """);
    }

    private static DiagnosticResult Expect(string fixture) =>
        new DiagnosticResult(TestFixtureNamesSolutionTypeAnalyzer.DiagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments(fixture);

    private static async Task Verify(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TestFixtureNamesSolutionTypeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        test.TestState.AdditionalReferences.Add(typeof(TestFixtureAttribute).Assembly);
        foreach (var diagnostic in expected)
        {
            test.ExpectedDiagnostics.Add(diagnostic);
        }

        await test.RunAsync();
    }
}
