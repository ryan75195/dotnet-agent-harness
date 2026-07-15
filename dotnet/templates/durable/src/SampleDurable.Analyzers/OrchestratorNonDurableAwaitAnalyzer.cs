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

    private const string ConfigureAwaitMethodName = "ConfigureAwait";

    private const string CompletedTaskPropertyName = "CompletedTask";

    private static readonly ImmutableHashSet<string> AllowedTaskCombinators =
        ImmutableHashSet.Create("WhenAll", "WhenAny", "FromResult", "Delay");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Non-durable await in orchestrator",
        "'{0}' is not a durable operation - orchestrators may only await TaskOrchestrationContext calls, Task.WhenAll, or Task.WhenAny; move the call into an activity and await context.CallActivityAsync(nameof(TheActivity), input)",
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
        context.RegisterSyntaxNodeAction(
            AnalyzeAwaitForEach,
            SyntaxKind.ForEachStatement,
            SyntaxKind.ForEachVariableStatement);
        context.RegisterSyntaxNodeAction(AnalyzeAwaitUsingDeclaration, SyntaxKind.LocalDeclarationStatement);
        context.RegisterSyntaxNodeAction(AnalyzeAwaitUsingStatement, SyntaxKind.UsingStatement);
    }

    private static void AnalyzeAwait(SyntaxNodeAnalysisContext context)
    {
        var awaitExpression = (AwaitExpressionSyntax)context.Node;
        AnalyzeAwaitedExpression(awaitExpression, awaitExpression.Expression, context);
    }

    private static void AnalyzeAwaitForEach(SyntaxNodeAnalysisContext context)
    {
        var statement = (CommonForEachStatementSyntax)context.Node;
        if (statement.AwaitKeyword.IsKind(SyntaxKind.None))
        {
            return;
        }

        AnalyzeAwaitedValue(statement, statement.Expression, context);
    }

    private static void AnalyzeAwaitUsingDeclaration(SyntaxNodeAnalysisContext context)
    {
        var statement = (LocalDeclarationStatementSyntax)context.Node;
        if (statement.AwaitKeyword.IsKind(SyntaxKind.None))
        {
            return;
        }

        AnalyzeDeclaredValues(statement, statement.Declaration, context);
    }

    private static void AnalyzeAwaitUsingStatement(SyntaxNodeAnalysisContext context)
    {
        var statement = (UsingStatementSyntax)context.Node;
        if (statement.AwaitKeyword.IsKind(SyntaxKind.None))
        {
            return;
        }

        AnalyzeDeclaredValues(statement, statement.Declaration, context);
        AnalyzeAwaitedValue(statement, statement.Expression, context);
    }

    private static void AnalyzeDeclaredValues(
        SyntaxNode node,
        VariableDeclarationSyntax? declaration,
        SyntaxNodeAnalysisContext context)
    {
        if (declaration == null)
        {
            return;
        }

        foreach (var declarator in declaration.Variables)
        {
            AnalyzeAwaitedValue(node, declarator.Initializer?.Value, context);
        }
    }

    private static void AnalyzeAwaitedValue(
        SyntaxNode node,
        ExpressionSyntax? expression,
        SyntaxNodeAnalysisContext context)
    {
        if (expression == null || expression is AwaitExpressionSyntax)
        {
            return;
        }

        AnalyzeAwaitedExpression(node, expression, context);
    }

    private static void AnalyzeAwaitedExpression(
        SyntaxNode node,
        ExpressionSyntax expression,
        SyntaxNodeAnalysisContext context)
    {
        if (!IsInsideOrchestrator(node, context.SemanticModel))
        {
            return;
        }

        if (IsDurableExpression(expression, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, expression.GetLocation(), Describe(expression)));
    }

    private static bool IsInsideOrchestrator(SyntaxNode node, SemanticModel model)
    {
        var enclosingMethod = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (enclosingMethod == null)
        {
            return false;
        }

        return DurableAnalyzerConstants.IsOrchestratorMethod(model.GetDeclaredSymbol(enclosingMethod));
    }

    private static bool IsDurableExpression(ExpressionSyntax expression, SemanticModel model)
    {
        switch (expression)
        {
            case InvocationExpressionSyntax invocation:
                var receiver = GetConfigureAwaitReceiver(invocation);
                return receiver != null
                    ? IsDurableExpression(receiver, model)
                    : IsDurableInvocation(invocation, model);

            case IdentifierNameSyntax:
            case MemberAccessExpressionSyntax:
                return IsAllowedTaskProperty(expression, model)
                    || IsDurableLocal(expression, model);

            case ElementAccessExpressionSyntax:
                return DurableAnalyzerConstants.IsTaskType(model.GetTypeInfo(expression).Type);

            case ParenthesizedExpressionSyntax parenthesized:
                return IsDurableExpression(parenthesized.Expression, model);

            default:
                return false;
        }
    }

    private static ExpressionSyntax? GetConfigureAwaitReceiver(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Name.Identifier.ValueText == ConfigureAwaitMethodName)
        {
            return memberAccess.Expression;
        }

        return null;
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
            return InlineTaskArgumentsAreDurable(invocation, model);
        }

        return DurableAnalyzerConstants.IsOrchestratorMethod(symbol)
            || IsDurableReceiver(symbol);
    }

    private static bool InlineTaskArgumentsAreDurable(
        InvocationExpressionSyntax invocation,
        SemanticModel model)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is not InvocationExpressionSyntax inner)
            {
                continue;
            }

            if (!DurableAnalyzerConstants.IsTaskType(model.GetTypeInfo(inner).Type))
            {
                continue;
            }

            if (!IsDurableExpression(inner, model))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllowedTaskCombinator(IMethodSymbol symbol)
    {
        var containing = symbol.ContainingType?.ToDisplayString();
        if (containing != DurableAnalyzerConstants.TaskType)
        {
            return false;
        }

        return AllowedTaskCombinators.Contains(symbol.Name);
    }

    private static bool IsAllowedTaskProperty(ExpressionSyntax expression, SemanticModel model)
    {
        if (model.GetSymbolInfo(expression).Symbol is not IPropertySymbol property)
        {
            return false;
        }

        return property.ContainingType?.ToDisplayString() == DurableAnalyzerConstants.TaskType
            && property.Name == CompletedTaskPropertyName;
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

        var declaringSyntax = local.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .FirstOrDefault();

        if (declaringSyntax is ForEachStatementSyntax)
        {
            return DurableAnalyzerConstants.IsTaskType(local.Type);
        }

        var initializer = (declaringSyntax as VariableDeclaratorSyntax)?.Initializer?.Value;
        return initializer != null && IsDurableExpression(initializer, model);
    }

    private static string Describe(ExpressionSyntax expression)
    {
        var text = expression.ToString();
        return text.Length > 60 ? text.Substring(0, 57) + "..." : text;
    }
}
