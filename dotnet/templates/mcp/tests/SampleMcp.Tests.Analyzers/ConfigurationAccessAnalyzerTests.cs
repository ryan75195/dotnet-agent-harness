using SampleMcp.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace SampleMcp.Tests.Analyzers;

[TestFixture]
public class ConfigurationAccessAnalyzerTests
{
    private const string ConfigurationStubs = @"
namespace Microsoft.Extensions.Configuration
{
    public interface IConfiguration
    {
        string? this[string key] { get; }
    }

    public interface IConfigurationSection : IConfiguration
    {
    }

    public sealed class ConfigurationManager : IConfiguration
    {
        public string? this[string key] => null;
    }

    public sealed class HostBuilderStub
    {
        public ConfigurationManager Configuration { get; } = new();
    }
}";

    [Test]
    public async Task Should_report_configuration_injected_into_a_service()
    {
        var source = @"
using Microsoft.Extensions.Configuration;

public sealed class PricingService
{
    private readonly {|#0:IConfiguration|} _configuration;

    public PricingService({|#1:IConfiguration|} configuration)
    {
        _configuration = configuration;
    }
}";

        await RunAsync(
            source,
            Expected(0, "IConfiguration"),
            Expected(1, "IConfiguration"));
    }

    [Test]
    public async Task Should_report_a_configuration_section_type_outside_the_root()
    {
        var source = @"
using Microsoft.Extensions.Configuration;

public sealed class Reader
{
    public string? Read({|#0:IConfigurationSection|} section) => section[""Key""];
}";

        await RunAsync(source, Expected(0, "IConfigurationSection"));
    }

    [Test]
    public async Task Should_report_a_configuration_property_reached_through_another_object()
    {
        var source = @"
using Microsoft.Extensions.Configuration;

public sealed class Reader
{
    public string? Read(HostBuilderStub builder) => builder.{|#0:Configuration|}[""Key""];
}";

        await RunAsync(source, Expected(0, "Configuration"));
    }

    [Test]
    public async Task Should_report_environment_variable_reads_outside_the_root()
    {
        var source = @"
public sealed class KeyReader
{
    public string? Read() => {|#0:System.Environment.GetEnvironmentVariable(""API_KEY"")|};
}";

        await RunAsync(source, Expected(0, "Environment.GetEnvironmentVariable"));
    }

    [Test]
    public async Task Should_allow_configuration_in_a_static_service_collection_extensions_class()
    {
        var source = @"
using Microsoft.Extensions.Configuration;

public static class ServiceCollectionExtensions
{
    public static string? Read(IConfiguration configuration) =>
        configuration[""Key""] ?? System.Environment.GetEnvironmentVariable(""KEY"");
}";

        await RunAsync(source);
    }

    [Test]
    public async Task Should_allow_configuration_in_top_level_statements()
    {
        var source = @"
using Microsoft.Extensions.Configuration;

IConfiguration configuration = new ConfigurationManager();
var key = configuration[""Key""] ?? System.Environment.GetEnvironmentVariable(""KEY"");
System.Console.WriteLine(key);";

        await RunAsync(source, outputKind: Microsoft.CodeAnalysis.OutputKind.WindowsApplication);
    }

    [Test]
    public async Task Should_report_a_non_static_class_named_like_the_composition_root()
    {
        var source = @"
using Microsoft.Extensions.Configuration;

public sealed class ServiceCollectionExtensions
{
    public string? Read({|#0:IConfiguration|} configuration) => configuration[""Key""];
}";

        await RunAsync(source, Expected(0, "IConfiguration"));
    }

    [Test]
    public async Task Should_allow_other_environment_members()
    {
        var source = @"
public sealed class Machine
{
    public string Name() => System.Environment.MachineName;
}";

        await RunAsync(source);
    }

    private static DiagnosticResult Expected(int location, string name) =>
        new DiagnosticResult(ConfigurationAccessAnalyzer.DiagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithLocation(location)
            .WithArguments(name);

    private static Task RunAsync(string source, params DiagnosticResult[] expected) =>
        RunAsync(source, Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary, expected);

    private static async Task RunAsync(
        string source,
        Microsoft.CodeAnalysis.OutputKind outputKind,
        params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ConfigurationAccessAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source, ConfigurationStubs }, OutputKind = outputKind },
        };
        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync();
    }
}
