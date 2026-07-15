using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SampleDurable.Analyzers;

internal static class DurableAnalyzerConstants
{
    internal const string OrchestrationTriggerAttribute =
        "Microsoft.Azure.Functions.Worker.OrchestrationTriggerAttribute";

    internal const string TaskOrchestrationContextType =
        "Microsoft.DurableTask.TaskOrchestrationContext";

    internal const string DurableNamespacePrefix = "Microsoft.DurableTask";

    internal static bool IsOrchestratorMethod(IMethodSymbol? method)
    {
        if (method == null)
        {
            return false;
        }

        return method.Parameters.Any(HasOrchestrationTrigger);
    }

    private static bool HasOrchestrationTrigger(IParameterSymbol parameter) =>
        parameter.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == OrchestrationTriggerAttribute);

    internal static bool IsOrchestrationContext(ITypeSymbol? type) =>
        type?.ToDisplayString() == TaskOrchestrationContextType;

    internal static bool IsDurableType(ITypeSymbol? type)
    {
        var ns = type?.ContainingNamespace?.ToDisplayString();
        if (ns == null)
        {
            return false;
        }

        return ns == DurableNamespacePrefix
            || ns.StartsWith(DurableNamespacePrefix + ".", StringComparison.Ordinal);
    }
}
