using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ConsoleApp.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NoChainedNullInArgumentAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0012";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not chain null-conditional and null-coalescing in method arguments",
        "Method argument contains chained '?.' and '??' — extract to a variable first",
        "Readability",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeArgument, SyntaxKind.Argument);
    }

    private static void AnalyzeArgument(SyntaxNodeAnalysisContext context)
    {
        var argument = (ArgumentSyntax)context.Node;

        if (!IsInsideInvocation(argument))
        {
            return;
        }

        var hasConditionalAccess = ContainsNodeDirectly(argument, SyntaxKind.ConditionalAccessExpression);
        var hasCoalesce = ContainsNodeDirectly(argument, SyntaxKind.CoalesceExpression);

        if (hasConditionalAccess && hasCoalesce)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, argument.GetLocation()));
        }
    }

    private static bool IsInsideInvocation(ArgumentSyntax argument)
    {
        return argument.Parent is ArgumentListSyntax { Parent: InvocationExpressionSyntax }
            or ArgumentListSyntax { Parent: ObjectCreationExpressionSyntax };
    }

    private static bool ContainsNodeDirectly(SyntaxNode root, SyntaxKind kind)
    {
        return SearchDescendants(root, kind);
    }

    private static bool SearchDescendants(SyntaxNode node, SyntaxKind kind)
    {
        foreach (var child in node.ChildNodes())
        {
            if (IsNestedScope(child))
            {
                continue;
            }

            if (child.IsKind(kind))
            {
                return true;
            }

            if (SearchDescendants(child, kind))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNestedScope(SyntaxNode node)
    {
        return node is LambdaExpressionSyntax
            or AnonymousMethodExpressionSyntax
            or SwitchExpressionSyntax;
    }
}
