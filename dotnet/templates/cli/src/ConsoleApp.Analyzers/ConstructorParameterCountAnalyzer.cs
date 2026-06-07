using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ConsoleApp.Analyzers;

/// <summary>
/// CI0005: Flags constructors with more than 5 parameters.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ConstructorParameterCountAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0005";
    public const int MaxConstructorParameters = 5;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Too many constructor parameters",
        "'{0}' constructor has {1} parameters (max {2}) — consider splitting responsibilities",
        "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
    }

    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        var constructor = (ConstructorDeclarationSyntax)context.Node;

        if (constructor.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return;
        }

        if (constructor.Parent is RecordDeclarationSyntax)
        {
            return;
        }

        if (constructor.Parent is TypeDeclarationSyntax containingType)
        {
            var typeSymbol = context.SemanticModel.GetDeclaredSymbol(containingType);
            if (typeSymbol != null)
            {
                if (typeSymbol.GetAttributes().Any(a =>
                    a.AttributeClass?.Name == "TestFixtureAttribute"))
                {
                    return;
                }

                var ns = typeSymbol.ContainingNamespace?.ToDisplayString() ?? "";
                if (ns.Contains(".Tests.") || ns.EndsWith(".Tests"))
                {
                    return;
                }
            }
        }

        var paramCount = constructor.ParameterList.Parameters.Count;
        if (paramCount > MaxConstructorParameters)
        {
            var typeName = constructor.Parent is BaseTypeDeclarationSyntax typeDecl
                ? typeDecl.Identifier.Text
                : "Unknown";

            context.ReportDiagnostic(
                Diagnostic.Create(Rule, constructor.GetLocation(),
                    typeName, paramCount, MaxConstructorParameters));
        }
    }
}
