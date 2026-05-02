using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ConsoleApp.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PublicMethodCountAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0004";
    public const int MaxPublicMethods = 10;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Too many public methods",
        "'{0}' declares {1} public methods (max {2}) — consider splitting responsibilities",
        "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class)
        {
            return;
        }

        if (type.IsRecord)
        {
            return;
        }

        // Exclude abstract classes, but NOT static classes.
        // Static classes are IsAbstract && IsStatic in Roslyn.
        if (type.IsAbstract && !type.IsStatic)
        {
            return;
        }

        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns.Contains(".Tests.") || ns.EndsWith(".Tests"))
        {
            return;
        }

        if (type.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "TestFixtureAttribute"))
        {
            return;
        }

        var publicMethodCount = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Count(m => m.DeclaredAccessibility == Accessibility.Public
                && m.MethodKind == MethodKind.Ordinary
                && !m.IsOverride
                && !AnalyzerConstants.ExcludedMethodNames.Contains(m.Name));

        if (publicMethodCount > MaxPublicMethods)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, type.Locations[0],
                    type.Name, publicMethodCount, MaxPublicMethods));
        }
    }
}
