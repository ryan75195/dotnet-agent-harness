using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SampleDurable.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ClassLengthAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0014";
    public const int MaxLines = 200;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Class too long",
        "'{0}' is {1} lines long (max {2}) â€” split into smaller focused classes",
        "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeType,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration);
    }

    private static void AnalyzeType(SyntaxNodeAnalysisContext context)
    {
        var typeDecl = (TypeDeclarationSyntax)context.Node;

        if (IsInTestContext(typeDecl, context.SemanticModel))
        {
            return;
        }

        var lineCount = CountEffectiveLines(typeDecl, context.Node.SyntaxTree);
        if (lineCount > MaxLines)
        {
            var location = typeDecl.Identifier.GetLocation();
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, location, typeDecl.Identifier.Text, lineCount, MaxLines));
        }
    }

    private static bool IsInTestContext(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel)
    {
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

    private static int CountEffectiveLines(TypeDeclarationSyntax typeDecl, SyntaxTree tree)
    {
        var text = tree.GetText();
        var span = typeDecl.Span;

        var startLine = text.Lines.GetLineFromPosition(span.Start).LineNumber;
        var endLine = text.Lines.GetLineFromPosition(span.End).LineNumber;

        startLine++;
        endLine--;

        var count = 0;
        for (var i = startLine; i <= endLine; i++)
        {
            var line = text.Lines[i];
            var lineText = line.ToString().Trim();

            if (lineText.Length == 0)
            {
                continue;
            }

            if (lineText.StartsWith("//"))
            {
                continue;
            }

            count++;
        }

        return count;
    }
}
