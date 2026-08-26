using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EtlApi.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TestFixtureNamesSolutionTypeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0019";

    private const string FixtureSuffix = "Tests";

    private const string UnitTestNamespaceSuffix = ".Tests.Unit";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Name a unit-test fixture after the type it tests",
        "Unit-test fixture '{0}' names no type in this solution - name it '<TypeUnderTest>Tests'",
        "Testing",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A fixture that does not mirror a type in this solution drifts away from its "
            + "subject unnoticed. A framework or library type does not count: the fixture must name "
            + "a type this solution owns.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var owned = OwnedTypeNames(start.Compilation);
            start.RegisterSymbolAction(symbol => Inspect(symbol, owned), SymbolKind.NamedType);
        });
    }

    private static void Inspect(SymbolAnalysisContext context, ImmutableHashSet<string> owned)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!IsUnitTestFixture(type) || type.Locations.Length == 0)
        {
            return;
        }

        if (!type.Name.EndsWith(FixtureSuffix, System.StringComparison.Ordinal))
        {
            Report(context, type);
            return;
        }

        var subject = type.Name.Substring(0, type.Name.Length - FixtureSuffix.Length);
        if (subject.Length == 0 || !owned.Contains(subject))
        {
            Report(context, type);
        }
    }

    private static void Report(SymbolAnalysisContext context, INamedTypeSymbol type) =>
        context.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations[0], type.Name));

    private static bool IsUnitTestFixture(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Class
        && HasTestFixtureAttribute(type)
        && type.ContainingNamespace is { IsGlobalNamespace: false }
        && type.ContainingNamespace.ToDisplayString().EndsWith(UnitTestNamespaceSuffix, System.StringComparison.Ordinal);

    private static bool HasTestFixtureAttribute(INamedTypeSymbol type) =>
        type.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "NUnit.Framework.TestFixtureAttribute");

    private static ImmutableHashSet<string> OwnedTypeNames(Compilation compilation)
    {
        var root = RootNamespace(compilation);
        var names = ImmutableHashSet.CreateBuilder<string>(System.StringComparer.Ordinal);
        Collect(compilation.SourceModule.GlobalNamespace, names);

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (!IsOwnedAssembly(assembly.Name, root))
            {
                continue;
            }

            Collect(assembly.GlobalNamespace, names);
        }

        return names.ToImmutable();
    }

    private static void Collect(INamespaceSymbol root, ImmutableHashSet<string>.Builder names)
    {
        foreach (var type in root.GetTypeMembers())
        {
            names.Add(type.Name);
        }

        foreach (var nested in root.GetNamespaceMembers())
        {
            Collect(nested, names);
        }
    }

    private static bool IsOwnedAssembly(string assemblyName, string root) =>
        assemblyName == root
        || assemblyName.StartsWith(root + ".", System.StringComparison.Ordinal);

    private static string RootNamespace(Compilation compilation)
    {
        var assemblyName = compilation.AssemblyName ?? string.Empty;
        var separator = assemblyName.IndexOf('.');
        return separator < 0 ? assemblyName : assemblyName.Substring(0, separator);
    }
}
