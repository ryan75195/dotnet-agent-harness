using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GlobalRealEstate.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NoAssertIgnoreAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0010";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not use Assert.Ignore",
        "Do not skip tests with Assert.Ignore — tests should fail loudly when preconditions are not met",
        "Testing",
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

        if (memberAccess.Name.Identifier.Text != "Ignore")
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol;
        if (symbol is not IMethodSymbol method)
        {
            return;
        }

        var containingType = method.ContainingType?.ToDisplayString();
        if (containingType == "NUnit.Framework.Assert")
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        }
    }
}
