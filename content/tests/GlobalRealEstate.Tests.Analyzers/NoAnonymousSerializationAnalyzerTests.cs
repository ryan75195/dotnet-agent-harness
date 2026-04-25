using GlobalRealEstate.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace GlobalRealEstate.Tests.Analyzers;

[TestFixture]
public class NoAnonymousSerializationAnalyzerTests
{
    [Test]
    public async Task Should_report_anonymous_object_in_serialize()
    {
        var source = """
            using System.Text.Json;

            public class MyService
            {
                public string Emit()
                {
                    return JsonSerializer.Serialize(new { tool = "Search", input = new { query = "test" } });
                }
            }
            """;

        var expected = DiagnosticResult.CompilerError("CI0011")
            .WithSpan(7, 41, 7, 96)
            .WithArguments("Serialize");

        var test = new CSharpAnalyzerTest<NoAnonymousSerializationAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_named_type_in_serialize()
    {
        var source = """
            using System.Text.Json;

            public class ToolCall
            {
                public string Tool { get; set; }
                public string Query { get; set; }
            }

            public class MyService
            {
                public string Emit()
                {
                    return JsonSerializer.Serialize(new ToolCall { Tool = "Search", Query = "test" });
                }
            }
            """;

        var test = new CSharpAnalyzerTest<NoAnonymousSerializationAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_serialize_on_non_anonymous_types()
    {
        var source = """
            using System.Text.Json;
            using System.Collections.Generic;

            public class MyService
            {
                public string Emit()
                {
                    var list = new List<string> { "a", "b" };
                    return JsonSerializer.Serialize(list);
                }
            }
            """;

        var test = new CSharpAnalyzerTest<NoAnonymousSerializationAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_serialize_with_string_argument()
    {
        var source = """
            using System.Text.Json;

            public class MyService
            {
                public string Emit(string data)
                {
                    return JsonSerializer.Serialize(data);
                }
            }
            """;

        var test = new CSharpAnalyzerTest<NoAnonymousSerializationAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }
}
