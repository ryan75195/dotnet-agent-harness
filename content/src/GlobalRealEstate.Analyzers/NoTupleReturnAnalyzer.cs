using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GlobalRealEstate.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NoTupleReturnAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not return tuple types",
        "'{0}' returns a tuple type — use a named type instead",
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
        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (ContainsTuple(method.ReturnType, context.SemanticModel))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, method.GetLocation(), method.Identifier.Text));
        }
    }

    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (ContainsTuple(property.Type, context.SemanticModel))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, property.GetLocation(), property.Identifier.Text));
        }
    }

    private static bool ContainsTuple(TypeSyntax typeSyntax, SemanticModel semanticModel)
    {
        var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
        var type = typeInfo.Type;
        if (type == null)
        {
            return false;
        }

        return ContainsTupleType(type);
    }

    private static bool ContainsTupleType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            if (namedType.IsTupleType)
            {
                return true;
            }

            // Check generic type arguments (e.g. Task<(int, string)>, IEnumerable<(int, string)>)
            foreach (var arg in namedType.TypeArguments)
            {
                if (ContainsTupleType(arg))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
