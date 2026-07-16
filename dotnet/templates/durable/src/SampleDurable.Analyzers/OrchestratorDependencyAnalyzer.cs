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
        "Orchestrator class has mutable state",
        "'{0}' declares an orchestrator but holds mutable state — orchestrators are replayed, so hold no instance state and no mutable statics (const and static readonly are fine), and reach the outside world only through activities",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Injected dependencies let an orchestrator perform non-replayable work, and a static field mutated during an orchestration yields different values across replays.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeType,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.RecordDeclaration);
    }

    private static void AnalyzeType(SyntaxNodeAnalysisContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;

        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
        if (symbol == null || !DeclaresOrchestrator(symbol))
        {
            return;
        }

        if (!HasInstanceState(symbol))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, typeDeclaration.Identifier.GetLocation(), symbol.Name));
    }

    private static bool DeclaresOrchestrator(INamedTypeSymbol symbol) =>
        symbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(DurableAnalyzerConstants.IsOrchestratorMethod);

    private static bool HasInstanceState(INamedTypeSymbol symbol)
    {
        if (HasOwnState(symbol, includePrivate: true))
        {
            return true;
        }

        var hasConstructorParameters = symbol.InstanceConstructors
            .Any(c => !c.IsImplicitlyDeclared && c.Parameters.Length > 0);

        if (hasConstructorParameters)
        {
            return true;
        }

        var baseType = symbol.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            if (HasOwnState(baseType, includePrivate: false))
            {
                return true;
            }

            baseType = baseType.BaseType;
        }

        return false;
    }

    private static bool HasOwnState(INamedTypeSymbol symbol, bool includePrivate) =>
        HasOwnInstanceState(symbol, includePrivate) || HasOwnMutableStaticState(symbol, includePrivate);

    private static bool HasOwnInstanceState(INamedTypeSymbol symbol, bool includePrivate)
    {
        var hasInstanceField = symbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Any(f => !f.IsStatic && !f.IsConst && !f.IsImplicitlyDeclared
                && (includePrivate || f.DeclaredAccessibility != Accessibility.Private));

        var hasInstanceProperty = symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Any(p => !p.IsStatic && !p.IsImplicitlyDeclared
                && (includePrivate || p.DeclaredAccessibility != Accessibility.Private));

        return hasInstanceField || hasInstanceProperty;
    }

    private static bool HasOwnMutableStaticState(INamedTypeSymbol symbol, bool includePrivate)
    {
        var hasMutableStaticField = symbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Any(f => f.IsStatic && !f.IsConst && !f.IsReadOnly && !f.IsImplicitlyDeclared
                && (includePrivate || f.DeclaredAccessibility != Accessibility.Private));

        var hasSettableStaticProperty = symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Any(p => p.IsStatic && !p.IsImplicitlyDeclared && p.SetMethod != null
                && (includePrivate || p.DeclaredAccessibility != Accessibility.Private));

        return hasMutableStaticField || hasSettableStaticProperty;
    }
}
