using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GlobalRealEstate.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NestedPublicTypeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0006";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Public type nested inside another type",
        "'{0}' is a public type nested inside '{1}' — move it to its own file",
        "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNestedType,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.EnumDeclaration);
    }

    private static void AnalyzeNestedType(SyntaxNodeAnalysisContext context)
    {
        var typeDecl = (BaseTypeDeclarationSyntax)context.Node;

        if (!typeDecl.Modifiers.Any(SyntaxKind.PublicKeyword)
            && !typeDecl.Modifiers.Any(SyntaxKind.ProtectedKeyword))
        {
            return;
        }

        if (typeDecl.Parent is not TypeDeclarationSyntax parentType)
        {
            return;
        }

        var ns = GetNamespace(typeDecl);
        if (ns != null && (ns.Contains(".Tests.") || ns.EndsWith(".Tests")))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, typeDecl.Identifier.GetLocation(),
                typeDecl.Identifier.Text, parentType.Identifier.Text));
    }

    private static string? GetNamespace(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is BaseNamespaceDeclarationSyntax ns)
            {
                return ns.Name.ToString();
            }

            current = current.Parent;
        }

        return null;
    }
}
