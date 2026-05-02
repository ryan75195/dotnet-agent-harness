using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ConsoleApp.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PragmaWarningDisableAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0008";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not suppress code quality warnings with #pragma",
        "Do not suppress '{0}' with #pragma — fix the underlying issue instead",
        "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(AnalyzeSyntaxTree);
    }

    private static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        var pragmas = root.DescendantTrivia()
            .Where(t => t.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
            .Select(t => t.GetStructure())
            .OfType<PragmaWarningDirectiveTriviaSyntax>();

        foreach (var pragma in pragmas)
        {
            if (!pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            {
                continue;
            }

            foreach (var errorCode in pragma.ErrorCodes)
            {
                var code = errorCode.ToString();
                if (code.Equals(DiagnosticId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (code.StartsWith("CA", System.StringComparison.Ordinal)
                    || code.StartsWith("CI", System.StringComparison.Ordinal))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, errorCode.GetLocation(), code));
                }
            }
        }
    }
}
