using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace GlobalRealEstate.Tests.Architecture;

internal static class TestHelpers
{
    public static Assembly CoreAssembly => typeof(Core.AssemblyMarker).Assembly;
    public static Assembly EtlAssembly => typeof(Etl.AssemblyMarker).Assembly;
    public static Assembly ApiAssembly => typeof(Api.AssemblyMarker).Assembly;

    public static readonly Assembly[] TestAssemblies =
    [
        typeof(LayerDependencyTests).Assembly,
        typeof(Tests.Unit.AssemblyMarker).Assembly,
        typeof(Tests.Integration.AssemblyMarker).Assembly,
        typeof(Tests.Analyzers.NoTupleReturnAnalyzerTests).Assembly
    ];

    public static readonly string[] ServiceNamespaces =
    [
        "GlobalRealEstate.Core.Data"
    ];

    public static bool IsRecord(Type type) =>
        type.GetMethod("<Clone>$") != null;

    public static bool IsDbContext(Type type) =>
        typeof(DbContext).IsAssignableFrom(type);
}
