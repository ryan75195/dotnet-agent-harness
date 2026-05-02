using EtlApi.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace EtlApi.Tests.Analyzers;

[TestFixture]
public class PublicMethodCountAnalyzerTests
{
    [Test]
    public async Task Should_report_when_class_exceeds_public_method_limit()
    {
        var methods = string.Join("\n",
            Enumerable.Range(1, 11).Select(i => $"        public void Method{i}() {{ }}"));

        var source = $$"""
            public class BigService
            {
            {{methods}}
            }
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0004")
            .WithSpan(1, 14, 1, 24)
            .WithArguments("BigService", 11, 10);

        var test = new CSharpAnalyzerTest<PublicMethodCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_when_class_is_within_limit()
    {
        var methods = string.Join("\n",
            Enumerable.Range(1, 10).Select(i => $"        public void Method{i}() {{ }}"));

        var source = $$"""
            public class OkService
            {
            {{methods}}
            }
            """;

        var test = new CSharpAnalyzerTest<PublicMethodCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_records()
    {
        var methods = string.Join("\n",
            Enumerable.Range(1, 11).Select(i => $"        public void Method{i}() {{ }}"));

        var source = $$"""
            public record BigRecord
            {
            {{methods}}
            }
            """;

        var test = new CSharpAnalyzerTest<PublicMethodCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_test_fixtures()
    {
        var methods = string.Join("\n",
            Enumerable.Range(1, 11).Select(i => $"        public void Method{i}() {{ }}"));

        var source = $$"""
            namespace NUnit.Framework
            {
                public class TestFixtureAttribute : System.Attribute { }
            }

            namespace EtlApi.Tests.Unit
            {
                [NUnit.Framework.TestFixture]
                public class BigTests
                {
            {{methods}}
                }
            }
            """;

        var test = new CSharpAnalyzerTest<PublicMethodCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_test_namespace_without_attribute()
    {
        var methods = string.Join("\n",
            Enumerable.Range(1, 11).Select(i => $"        public void Method{i}() {{ }}"));

        var source = $$"""
            namespace EtlApi.Tests.Unit
            {
                public class SomeBigTestHelper
                {
            {{methods}}
                }
            }
            """;

        var test = new CSharpAnalyzerTest<PublicMethodCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_exclude_overrides_and_framework_methods()
    {
        var methods = string.Join("\n",
            Enumerable.Range(1, 8).Select(i => $"        public void Method{i}() {{ }}"));

        var source = $$"""
            public class MyService
            {
            {{methods}}
                public void Dispose() { }
                public override string ToString() => "";
                public override int GetHashCode() => 0;
            }
            """;

        var test = new CSharpAnalyzerTest<PublicMethodCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_for_static_class_exceeding_limit()
    {
        var methods = string.Join("\n",
            Enumerable.Range(1, 11).Select(i => $"        public static void Method{i}() {{ }}"));

        var source = $$"""
            public static class BigStaticService
            {
            {{methods}}
            }
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0004")
            .WithSpan(1, 21, 1, 37)
            .WithArguments("BigStaticService", 11, 10);

        var test = new CSharpAnalyzerTest<PublicMethodCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_abstract_class()
    {
        var methods = string.Join("\n",
            Enumerable.Range(1, 11).Select(i => $"        public abstract void Method{i}();"));

        var source = $$"""
            public abstract class BigAbstractService
            {
            {{methods}}
            }
            """;

        var test = new CSharpAnalyzerTest<PublicMethodCountAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }
}
