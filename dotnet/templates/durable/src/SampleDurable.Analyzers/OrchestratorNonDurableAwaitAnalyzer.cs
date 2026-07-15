using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SampleDurable.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class OrchestratorNonDurableAwaitAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0016";

    private static readonly ImmutableHashSet<string> AllowedTaskCombinators =
        ImmutableHashSet.Create("WhenAll", "WhenAny", "FromResult", "CompletedTask", "Delay");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Non-durable await in orchestrator",
        "'{0}' is not a durable operation - orchestrators may only await TaskOrchestrationContext calls, Task.WhenAll, or Task.WhenAny",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Awaiting anything other than a durable operation breaks orchestration replay.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeAwait, SyntaxKind.AwaitExpression);
    }

    private static void AnalyzeAwait(SyntaxNodeAnalysisContext context)
    {
        var awaitExpression = (AwaitExpressionSyntax)context.Node;

        var enclosingMethod = awaitExpression.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (enclosingMethod == null)
        {
            return;
        }

        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(enclosingMethod);
        if (!DurableAnalyzerConstants.IsOrchestratorMethod(methodSymbol))
        {
            return;
        }

        if (IsDurableExpression(awaitExpression.Expression, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, awaitExpression.Expression.GetLocation(),
                Describe(awaitExpression.Expression)));
    }

    private static bool IsDurableExpression(ExpressionSyntax expression, SemanticModel model)
    {
        switch (expression)
        {
            case InvocationExpressionSyntax invocation:
                return IsDurableInvocation(invocation, model);

            case IdentifierNameSyntax:
            case MemberAccessExpressionSyntax:
                return IsDurableLocal(expression, model);

            case ParenthesizedExpressionSyntax parenthesized:
                return IsDurableExpression(parenthesized.Expression, model);

            default:
                return false;
        }
    }

    private static bool IsDurableInvocation(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        var symbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (symbol == null)
        {
            return false;
        }

        if (IsAllowedTaskCombinator(symbol))
        {
            return true;
        }

        return IsDurableReceiver(symbol);
    }

    private static bool IsAllowedTaskCombinator(IMethodSymbol symbol)
    {
        var containing = symbol.ContainingType?.ToDisplayString();
        if (containing != "System.Threading.Tasks.Task")
        {
            return false;
        }

        return AllowedTaskCombinators.Contains(symbol.Name);
    }

    private static bool IsDurableReceiver(IMethodSymbol symbol)
    {
        if (symbol.IsExtensionMethod && DurableAnalyzerConstants.IsDurableType(symbol.ReceiverType))
        {
            return true;
        }

        var reduced = symbol.ReducedFrom ?? symbol;
        if (reduced.IsExtensionMethod
            && reduced.Parameters.Length > 0
            && DurableAnalyzerConstants.IsDurableType(reduced.Parameters[0].Type))
        {
            return true;
        }

        return DurableAnalyzerConstants.IsDurableType(symbol.ContainingType);
    }

    private static bool IsDurableLocal(ExpressionSyntax expression, SemanticModel model)
    {
        var symbol = model.GetSymbolInfo(expression).Symbol;
        if (symbol is not ILocalSymbol local)
        {
            return false;
        }

        var declarator = local.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        var initializer = declarator?.Initializer?.Value;
        return initializer != null && IsDurableExpression(initializer, model);
    }

    private static string Describe(ExpressionSyntax expression)
    {
        var text = expression.ToString();
        return text.Length > 60 ? text.Substring(0, 57) + "..." : text;
    }
}
