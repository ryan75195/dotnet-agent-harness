using EtlApi.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace EtlApi.Tests.Analyzers;

[TestFixture]
public class MethodLengthAnalyzerTests
{
    [Test]
    public async Task Should_report_when_method_exceeds_line_limit()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 41).Select(i => $"            var x{i} = {i};"));

        var source = $$"""
            public class MyService
            {
                public void LongMethod()
                {
            {{lines}}
                }
            }
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0007")
            .WithSpan(3, 17, 3, 27)
            .WithArguments("LongMethod", 41);

        var test = new CSharpAnalyzerTest<MethodLengthAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_when_method_is_within_limit()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 40).Select(i => $"            var x{i} = {i};"));

        var source = $$"""
            public class MyService
            {
                public void OkMethod()
                {
            {{lines}}
                }
            }
            """;

        var test = new CSharpAnalyzerTest<MethodLengthAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_count_blank_lines()
    {
        var codeLines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"            var x{i} = {i};"));
        var blankLines = string.Join("\n", Enumerable.Range(1, 25).Select(_ => ""));

        var source = $$"""
            public class MyService
            {
                public void MethodWithBlanks()
                {
            {{codeLines}}
            {{blankLines}}
                }
            }
            """;

        var test = new CSharpAnalyzerTest<MethodLengthAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_count_comment_lines()
    {
        var codeLines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"            var x{i} = {i};"));
        var commentLines = string.Join("\n", Enumerable.Range(1, 25).Select(i => $"            // comment {i}"));

        var source = $$"""
            public class MyService
            {
                public void MethodWithComments()
                {
            {{codeLines}}
            {{commentLines}}
                }
            }
            """;

        var test = new CSharpAnalyzerTest<MethodLengthAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_test_fixtures()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 41).Select(i => $"            var x{i} = {i};"));

        var source = $$"""
            namespace NUnit.Framework
            {
                public class TestFixtureAttribute : System.Attribute { }
            }

            namespace EtlApi.Tests.Unit
            {
                [NUnit.Framework.TestFixture]
                public class MyTests
                {
                    public void LongTestMethod()
                    {
            {{lines}}
                    }
                }
            }
            """;

        var test = new CSharpAnalyzerTest<MethodLengthAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_in_test_namespace()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 41).Select(i => $"            var x{i} = {i};"));

        var source = $$"""
            namespace EtlApi.Tests.Unit
            {
                public class SomeTestHelper
                {
                    public void LongHelperMethod()
                    {
            {{lines}}
                    }
                }
            }
            """;

        var test = new CSharpAnalyzerTest<MethodLengthAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }
}
