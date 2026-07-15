using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SampleDurable.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class OrchestratorDependencyAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0017";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Orchestrator class has instance state",
        "'{0}' declares an orchestrator but holds instance state — orchestrators must be stateless and reach the outside world only through activities",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Injected dependencies let an orchestrator perform non-replayable work.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
        if (symbol == null || !DeclaresOrchestrator(symbol))
        {
            return;
        }

        if (!HasInstanceState(symbol))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, classDeclaration.Identifier.GetLocation(), symbol.Name));
    }

    private static bool DeclaresOrchestrator(INamedTypeSymbol symbol) =>
        symbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(DurableAnalyzerConstants.IsOrchestratorMethod);

    private static bool HasInstanceState(INamedTypeSymbol symbol)
    {
        var hasInstanceField = symbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Any(f => !f.IsStatic && !f.IsConst && !f.IsImplicitlyDeclared);

        var hasInstanceProperty = symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Any(p => !p.IsStatic);

        var hasConstructorParameters = symbol.InstanceConstructors
            .Any(c => !c.IsImplicitlyDeclared && c.Parameters.Length > 0);

        return hasInstanceField || hasInstanceProperty || hasConstructorParameters;
    }
}
