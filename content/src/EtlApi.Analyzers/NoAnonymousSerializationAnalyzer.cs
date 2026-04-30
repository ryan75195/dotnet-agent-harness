using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EtlApi.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NoAnonymousSerializationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0011";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not serialize anonymous objects",
        "Anonymous object passed to {0} — use a named record instead",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var methodName = GetMethodName(invocation);

        if (methodName is null || !IsSerializationMethod(methodName))
        {
            return;
        }

        CheckArgumentsForAnonymousTypes(context, invocation, methodName);
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null,
        };
    }

    private static bool IsSerializationMethod(string? methodName)
    {
        return methodName == "Serialize" || methodName == "SerializeAsync";
    }

    private static void CheckArgumentsForAnonymousTypes(
        SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, string methodName)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is AnonymousObjectCreationExpressionSyntax)
            {
                ReportDiagnostic(context, argument, methodName);
                return;
            }

            if (!IsNestedAnonymousInNewExpression(argument.Expression, context.SemanticModel))
            {
                continue;
            }

            ReportDiagnostic(context, argument, methodName);
            return;
        }
    }

    private static bool IsNestedAnonymousInNewExpression(
        ExpressionSyntax expression, SemanticModel semanticModel)
    {
        if (expression is not ObjectCreationExpressionSyntax creation)
        {
            return false;
        }

        var typeInfo = semanticModel.GetTypeInfo(creation);
        return typeInfo.Type?.IsAnonymousType == true;
    }

    private static void ReportDiagnostic(
        SyntaxNodeAnalysisContext context, ArgumentSyntax argument, string methodName)
    {
        context.ReportDiagnostic(
            Diagnostic.Create(Rule, argument.GetLocation(), methodName));
    }
}
