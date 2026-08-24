using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ConsoleApp.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestFixtureSourceTypeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0019";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(new DiagnosticDescriptor(
            DiagnosticId,
            "Test fixture source type is missing",
            "Test fixture name must identify a type in this solution",
            "Testing",
            DiagnosticSeverity.Error,
            true));

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
    }
}
