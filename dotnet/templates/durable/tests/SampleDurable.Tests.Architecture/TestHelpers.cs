using System.CodeDom.Compiler;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace SampleDurable.Tests.Architecture;

internal static class TestHelpers
{
    private const string FunctionsWorkerSdkGeneratorTool = "Microsoft.Azure.Functions.Worker.Sdk.Generators";

    public static bool IsGeneratedCode(Type type) =>
        type.GetCustomAttribute<GeneratedCodeAttribute>()?.Tool == FunctionsWorkerSdkGeneratorTool;

    public static Assembly CoreAssembly => typeof(Core.AssemblyMarker).Assembly;
    public static Assembly FunctionsAssembly => typeof(Functions.AssemblyMarker).Assembly;

    public static readonly Assembly[] TestAssemblies =
    [
        typeof(LayerDependencyTests).Assembly,
        typeof(Tests.Unit.AssemblyMarker).Assembly,
        typeof(Tests.Integration.AssemblyMarker).Assembly,
        typeof(Tests.Analyzers.NoTupleReturnAnalyzerTests).Assembly
    ];

    public static readonly string[] ServiceNamespaces =
    [
        "SampleDurable.Core.Services"
    ];

    public static bool IsRecord(Type type) =>
        type.GetMethod("<Clone>$") != null;

    public static bool IsDbContext(Type type) =>
        typeof(DbContext).IsAssignableFrom(type);

    public static readonly (string Attribute, string Namespace)[] TriggerNamespaces =
    [
        ("OrchestrationTriggerAttribute", "Orchestrations"),
        ("ActivityTriggerAttribute", "Activities"),
        ("EntityTriggerAttribute", "Entities"),
        ("HttpTriggerAttribute", "Triggers")
    ];

    public static IEnumerable<MethodInfo> FunctionMethods() =>
        FunctionsAssembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "FunctionAttribute"));
}
