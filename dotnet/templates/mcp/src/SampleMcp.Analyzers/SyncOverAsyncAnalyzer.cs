using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SampleMcp.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class SyncOverAsyncAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0015";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Synchronous access on Task",
        "'{0}' blocks asynchronously — use 'await' instead of '.{1}'",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        var memberName = memberAccess.Name.Identifier.Text;

        if (memberName != "Result")
        {
            return;
        }

        if (memberAccess.Parent is InvocationExpressionSyntax)
        {
            return;
        }

        var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression);
        if (typeInfo.Type == null)
        {
            return;
        }

        if (IsTaskType(typeInfo.Type))
        {
            var containingExpression = GetContainingExpressionDescription(memberAccess.Expression);
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, memberAccess.Name.GetLocation(),
                    containingExpression, "Result"));
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.Text;

        if (methodName == "Wait")
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression);
            if (typeInfo.Type != null && IsTaskType(typeInfo.Type))
            {
                var containingExpression = GetContainingExpressionDescription(memberAccess.Expression);
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, memberAccess.Name.GetLocation(),
                        containingExpression, "Wait()"));
            }
        }
        else if (methodName == "GetResult")
        {
            if (memberAccess.Expression is InvocationExpressionSyntax getAwaiterCall
                && getAwaiterCall.Expression is MemberAccessExpressionSyntax getAwaiterAccess
                && getAwaiterAccess.Name.Identifier.Text == "GetAwaiter")
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(getAwaiterAccess.Expression);
                if (typeInfo.Type != null && IsTaskType(typeInfo.Type))
                {
                    var containingExpression = GetContainingExpressionDescription(getAwaiterAccess.Expression);
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, memberAccess.Name.GetLocation(),
                            containingExpression, "GetAwaiter().GetResult()"));
                }
            }
        }
    }

    private static bool IsTaskType(ITypeSymbol type)
    {
        var name = type.Name;
        if (name is "Task" or "ValueTask")
        {
            var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
            return ns is "System.Threading.Tasks";
        }

        return false;
    }

    private static string GetContainingExpressionDescription(ExpressionSyntax expression)
    {
        var text = expression.ToString();
        if (text.Length > 40)
        {
            text = text.Substring(0, 37) + "...";
        }

        return text;
    }
}
