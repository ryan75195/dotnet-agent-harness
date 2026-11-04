using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using EtlApi.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis;

namespace EtlApi.Tests.Analyzers;

[TestFixture]
public class OptionsDefaultsInJsonAnalyzerTests
{
    [Test]
    public async Task Should_report_literal_defaults_on_an_options_class()
    {
        var source = @"
public sealed class GeminiOptions
{
    public const string SectionName = ""App:Gemini"";

    public string Model { get; set; } = {|#0:""gemini-2.5-flash""|};

    public int MaxRetries { get; set; } = {|#1:3|};
}";

        await RunAsync(
            source,
            Expected(0, "GeminiOptions", "Model", "App:Gemini"),
            Expected(1, "GeminiOptions", "MaxRetries", "App:Gemini"));
    }

    [Test]
    public async Task Should_report_computed_defaults_and_field_defaults()
    {
        var source = @"
public sealed class UploadOptions
{
    public const string SectionName = ""Upload"";

    public System.TimeSpan Timeout { get; set; } = {|#0:System.TimeSpan.FromMinutes(5)|};

    public double Threshold = {|#1:0.7|};
}";

        await RunAsync(
            source,
            Expected(0, "UploadOptions", "Timeout", "Upload"),
            Expected(1, "UploadOptions", "Threshold", "Upload"));
    }

    [Test]
    public async Task Should_allow_empty_initialisers_that_only_satisfy_nullability()
    {
        var source = @"
using System.Collections.Generic;

public sealed class NestedOptions
{
}

public sealed class EmptyOptions
{
    public const string SectionName = ""Empty"";

    public string A { get; set; } = string.Empty;

    public string B { get; set; } = """";

    public string C { get; set; } = null!;

    public List<string> D { get; set; } = [];

    public NestedOptions E { get; set; } = new();

    public string[] F { get; set; } = System.Array.Empty<string>();

    public static int Shared { get; set; } = 4;
}";

        await RunAsync(source);
    }

    [Test]
    public async Task Should_ignore_classes_without_a_section_name()
    {
        var source = @"
public sealed class RenderOptions
{
    public int Width { get; set; } = 1080;
}

public sealed class Settings
{
    public const string SectionName = ""Settings"";

    public int Width { get; set; } = 1080;
}";

        await RunAsync(source);
    }

    [TestCase("true", 0)]
    [TestCase("TRUE", 0)]
    [TestCase("false", 1)]
    public async Task Should_allow_defaults_only_when_the_project_opts_out(string optOut, int expectedDiagnostics)
    {
        var source = @"
public sealed class GeminiOptions
{
    public const string SectionName = ""App:Gemini"";

    public string Model { get; set; } = ""gemini-2.5-flash"";
}";

        var compilation = CSharpCompilation.Create(
            "OptOutProbe",
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var provider = new GlobalOnlyOptionsProvider(
            new Dictionary<string, string> { [OptionsDefaultsInJsonAnalyzer.OptOutProperty] = optOut });

        var diagnostics = await compilation
            .WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new OptionsDefaultsInJsonAnalyzer()),
                new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, provider))
            .GetAnalyzerDiagnosticsAsync();

        Assert.That(diagnostics.Count(d => d.Id == OptionsDefaultsInJsonAnalyzer.DiagnosticId), Is.EqualTo(expectedDiagnostics));
    }

    private static DiagnosticResult Expected(int location, string type, string member, string section) =>
        new DiagnosticResult(OptionsDefaultsInJsonAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithLocation(location)
            .WithArguments(type, member, section);

    private static async Task RunAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<OptionsDefaultsInJsonAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source } },
        };
        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync();
    }

    private sealed class GlobalOnlyOptions(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }

    private sealed class GlobalOnlyOptionsProvider(Dictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        private readonly GlobalOnlyOptions _options = new(values);

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }
}
