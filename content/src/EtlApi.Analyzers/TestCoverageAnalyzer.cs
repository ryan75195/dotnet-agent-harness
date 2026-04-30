using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EtlApi.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TestCoverageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0002";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Public method lacks test coverage",
        "'{0}' does not test public method '{1}'",
        "Testing",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var fixtures = FindTestFixtures(context.Compilation);
        if (fixtures.IsEmpty)
        {
            return;
        }

        var coveredMethods = new ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>();
        foreach (var fixture in fixtures)
        {
            coveredMethods[fixture.fixtureType.Name] = new ConcurrentDictionary<string, byte>();
        }

        context.RegisterSyntaxNodeAction(
            ctx => OnInvocation(ctx, fixtures, coveredMethods),
            SyntaxKind.InvocationExpression);

        context.RegisterCompilationEndAction(
            ctx => OnCompilationEnd(ctx, fixtures, coveredMethods));
    }

    private static void OnInvocation(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<(INamedTypeSymbol fixtureType, INamedTypeSymbol sourceType)> fixtures,
        ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> coveredMethods)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol invokedMethod)
        {
            return;
        }

        var containingType = FindContainingType(context.Node);
        if (containingType == null)
        {
            return;
        }

        var containingSymbol = context.SemanticModel.GetDeclaredSymbol(containingType);
        if (containingSymbol == null)
        {
            return;
        }

        foreach (var (fixtureType, sourceType) in fixtures)
        {
            if (!SymbolEqualityComparer.Default.Equals(containingSymbol, fixtureType))
            {
                continue;
            }

            if (IsMethodOnSourceType(invokedMethod, sourceType))
            {
                coveredMethods[fixtureType.Name].TryAdd(invokedMethod.Name, 0);
            }
        }
    }

    private static void OnCompilationEnd(
        CompilationAnalysisContext context,
        ImmutableArray<(INamedTypeSymbol fixtureType, INamedTypeSymbol sourceType)> fixtures,
        ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> coveredMethods)
    {
        foreach (var (fixtureType, sourceType) in fixtures)
        {
            var requiredMethods = GetRequiredMethods(sourceType);
            var covered = coveredMethods[fixtureType.Name];

            foreach (var method in requiredMethods)
            {
                if (!covered.ContainsKey(method.Name))
                {
                    var location = fixtureType.Locations.FirstOrDefault() ?? Location.None;
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, location,
                            fixtureType.Name,
                            $"{sourceType.Name}.{method.Name}"));
                }
            }
        }
    }

    private static ImmutableArray<(INamedTypeSymbol fixtureType, INamedTypeSymbol sourceType)> FindTestFixtures(
        Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<(INamedTypeSymbol, INamedTypeSymbol)>();

        foreach (var type in GetAllTypes(compilation.GlobalNamespace))
        {
            if (!type.Name.EndsWith("Tests") || type.Name.Length <= 5)
            {
                continue;
            }

            if (type.TypeKind != TypeKind.Class || type.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            // Only consider types declared in the current compilation (not referenced assemblies)
            if (!type.Locations.Any(loc => loc.IsInSource
                && compilation.SyntaxTrees.Contains(loc.SourceTree)))
            {
                continue;
            }

            var sourceTypeName = type.Name.Substring(0, type.Name.Length - 5);
            var sourceType = FindSourceType(compilation, sourceTypeName, type);
            if (sourceType == null)
            {
                continue;
            }

            if (sourceType.IsStatic || sourceType.IsAbstract)
            {
                continue;
            }

            builder.Add((type, sourceType));
        }

        return builder.ToImmutable();
    }

    private static INamedTypeSymbol? FindSourceType(
        Compilation compilation, string name, INamedTypeSymbol fixtureType)
    {
        // Search current compilation (for verifier tests where both types are in same compilation)
        foreach (var type in GetAllTypes(compilation.GlobalNamespace))
        {
            if (type.Name == name
                && type.TypeKind == TypeKind.Class
                && !SymbolEqualityComparer.Default.Equals(type, fixtureType))
            {
                return type;
            }
        }

        // Search referenced assemblies
        foreach (var reference in compilation.References)
        {
            var symbol = compilation.GetAssemblyOrModuleSymbol(reference);
            if (symbol is IAssemblySymbol assembly)
            {
                foreach (var type in GetAllTypes(assembly.GlobalNamespace))
                {
                    if (type.Name == name && type.TypeKind == TypeKind.Class)
                    {
                        return type;
                    }
                }
            }
        }

        return null;
    }

    private static ImmutableArray<IMethodSymbol> GetRequiredMethods(INamedTypeSymbol type)
    {
        return type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public
                && m.MethodKind == MethodKind.Ordinary
                && !m.IsStatic
                && !AnalyzerConstants.ExcludedMethodNames.Contains(m.Name)
                && !m.IsOverride)
            .ToImmutableArray();
    }

    private static bool IsMethodOnSourceType(IMethodSymbol invokedMethod, INamedTypeSymbol sourceType)
    {
        var containingType = invokedMethod.ContainingType;
        if (containingType == null)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(containingType, sourceType))
        {
            return true;
        }

        // Check if called through an interface that sourceType implements
        foreach (var iface in sourceType.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(containingType, iface))
            {
                // Verify the interface has a method with this name
                if (iface.GetMembers(invokedMethod.Name).Any())
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ClassDeclarationSyntax? FindContainingType(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is ClassDeclarationSyntax classDecl)
            {
                return classDecl;
            }

            current = current.Parent;
        }
        return null;
    }

    private static System.Collections.Generic.IEnumerable<INamedTypeSymbol> GetAllTypes(
        INamespaceSymbol namespaceSymbol)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            yield return type;
        }

        foreach (var ns in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var type in GetAllTypes(ns))
            {
                yield return type;
            }
        }
    }
}
