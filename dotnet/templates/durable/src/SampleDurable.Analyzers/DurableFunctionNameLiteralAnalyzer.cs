using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SampleDurable.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DurableFunctionNameLiteralAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0018";

    private static readonly ImmutableHashSet<string> TargetMethods =
        ImmutableHashSet.Create("CallActivityAsync", "CallSubOrchestratorAsync");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Durable function name is a string literal",
        "'{0}' takes a string literal — use nameof(...) or a const so renames stay safe",
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

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name switch
        {
            GenericNameSyntax generic => generic.Identifier.Text,
            _ => memberAccess.Name.Identifier.Text
        };

        if (!TargetMethods.Contains(methodName))
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression).Type;
        if (!DurableAnalyzerConstants.IsOrchestrationContext(receiverType))
        {
            return;
        }

        var firstArgument = invocation.ArgumentList.Arguments.Count > 0
            ? invocation.ArgumentList.Arguments[0].Expression
            : null;

        if (firstArgument is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, literal.GetLocation(), methodName));
    }
}
