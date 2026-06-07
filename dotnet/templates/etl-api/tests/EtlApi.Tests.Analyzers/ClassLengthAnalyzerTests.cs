using EtlApi.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace EtlApi.Tests.Analyzers;

[TestFixture]
public class ClassLengthAnalyzerTests
{
    [Test]
    public async Task Should_report_class_exceeding_max_lines()
    {
        var methods = string.Join("\n", Enumerable.Range(1, 205)
            .Select(i => $"    public void Method{i}() {{ }}"));

        var source = $@"
public class BigService
{{
{methods}
}}";

        var test = new CSharpAnalyzerTest<ClassLengthAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source } },
        };

        test.ExpectedDiagnostics.Add(
            DiagnosticResult.CompilerWarning("CI0014")
                .WithSpan(2, 14, 2, 24));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_class_within_limit()
    {
        var source = @"
public class SmallService
{
    public void DoWork() { }
    public void DoMore() { }
}";

        var test = new CSharpAnalyzerTest<ClassLengthAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source } },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_skip_test_fixtures()
    {
        var methods = string.Join("\n", Enumerable.Range(1, 205)
            .Select(i => $"        public void Method{i}() {{ }}"));

        var source = $@"
namespace MyApp.Tests.Unit
{{
    public class BigServiceTests
    {{
{methods}
    }}
}}";

        var test = new CSharpAnalyzerTest<ClassLengthAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source } },
        };

        await test.RunAsync();
    }
}
