using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GlobalRealEstate.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MethodLengthAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0007";
    public const int MaxLines = 40;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Method too long",
        "'{0}' is {1} lines long (max 40) — extract helper methods",
        "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeLocalFunction, SyntaxKind.LocalFunctionStatement);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (IsInTestContext(method, context.SemanticModel))
        {
            return;
        }

        var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body == null)
        {
            return;
        }

        var lineCount = CountEffectiveLines(body, context.Node.SyntaxTree);
        if (lineCount > MaxLines)
        {
            var location = method.Identifier.GetLocation();
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, location, method.Identifier.Text, lineCount));
        }
    }

    private static void AnalyzeLocalFunction(SyntaxNodeAnalysisContext context)
    {
        var localFunc = (LocalFunctionStatementSyntax)context.Node;

        if (IsInTestContext(localFunc, context.SemanticModel))
        {
            return;
        }

        var body = (SyntaxNode?)localFunc.Body ?? localFunc.ExpressionBody;
        if (body == null)
        {
            return;
        }

        var lineCount = CountEffectiveLines(body, context.Node.SyntaxTree);
        if (lineCount > MaxLines)
        {
            var location = localFunc.Identifier.GetLocation();
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, location, localFunc.Identifier.Text, lineCount));
        }
    }

    private static bool IsInTestContext(SyntaxNode node, SemanticModel semanticModel)
    {
        // Walk up to the containing type declaration
        var typeDecl = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (typeDecl == null)
        {
            return false;
        }

        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl);
        if (typeSymbol == null)
        {
            return false;
        }

        var ns = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (ns.Contains(".Tests.") || ns.EndsWith(".Tests"))
        {
            return true;
        }

        return typeSymbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "TestFixtureAttribute");
    }

    private static int CountEffectiveLines(SyntaxNode body, SyntaxTree tree)
    {
        var text = tree.GetText();
        var bodySpan = body.Span;

        var startLine = text.Lines.GetLineFromPosition(bodySpan.Start).LineNumber;
        var endLine = text.Lines.GetLineFromPosition(bodySpan.End).LineNumber;

        // For block bodies, exclude the opening '{' and closing '}' lines
        var isBlock = body is BlockSyntax;
        if (isBlock)
        {
            startLine++;
            endLine--;
        }

        var count = 0;
        for (var i = startLine; i <= endLine; i++)
        {
            var line = text.Lines[i];
            var lineText = line.ToString().Trim();

            // Skip blank lines
            if (lineText.Length == 0)
            {
                continue;
            }

            // Skip comment-only lines
            if (lineText.StartsWith("//"))
            {
                continue;
            }

            count++;
        }

        return count;
    }
}
