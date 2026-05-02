using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ConsoleApp.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NoCommentsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0013";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Comments are not allowed",
        "Comments are not allowed — code should be self-documenting. Extract intent into method names, variable names, or types. If a WHY is genuinely non-obvious (hidden constraint, workaround for a specific bug), extract it into a named helper instead of a comment.",
        "Readability",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(AnalyzeTree);
    }

    private static void AnalyzeTree(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        foreach (var trivia in root.DescendantTrivia())
        {
            if (IsComment(trivia))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, trivia.GetLocation()));
            }
        }
    }

    private static bool IsComment(SyntaxTrivia trivia)
    {
        var kind = trivia.Kind();
        return kind == SyntaxKind.SingleLineCommentTrivia
            || kind == SyntaxKind.MultiLineCommentTrivia
            || kind == SyntaxKind.SingleLineDocumentationCommentTrivia
            || kind == SyntaxKind.MultiLineDocumentationCommentTrivia;
    }
}
