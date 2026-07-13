using System.Collections.Immutable;

namespace SampleMcp.Analyzers;

internal static class AnalyzerConstants
{
    internal static readonly ImmutableHashSet<string> ExcludedMethodNames =
        ImmutableHashSet.Create(
            "Dispose", "DisposeAsync", "ToString", "Equals",
            "GetHashCode", "GetType", "Finalize",
            "Start", "StartAsync", "StopAsync", "ExecuteAsync");
}
