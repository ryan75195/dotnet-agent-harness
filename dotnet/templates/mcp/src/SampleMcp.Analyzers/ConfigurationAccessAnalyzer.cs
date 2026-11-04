using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SampleMcp.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ConfigurationAccessAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0020";

    private const string ConfigurationInterface = "Microsoft.Extensions.Configuration.IConfiguration";
    private const string ConfigurationNamespace = "Microsoft.Extensions.Configuration";
    private const string EnvironmentType = "System.Environment";
    private const string CompositionRootSuffix = "ServiceCollectionExtensions";

    private static readonly ImmutableHashSet<string> EnvironmentReads =
        ImmutableHashSet.Create("GetEnvironmentVariable", "GetEnvironmentVariables");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Configuration read outside the composition root",
        "'{0}' reads configuration outside the composition root — bind an options class in Program.cs or a static *ServiceCollectionExtensions class and inject IOptions<T> instead",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeName, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeName(SyntaxNodeAnalysisContext context)
    {
        var name = (SimpleNameSyntax)context.Node;
        var symbol = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol;

        var readsConfiguration = symbol switch
        {
            INamedTypeSymbol type => IsConfigurationType(type),
            IPropertySymbol property => IsConfigurationType(property.Type),
            _ => false,
        };

        if (readsConfiguration && !IsInCompositionRoot(name))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), name.ToString()));
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!EnvironmentReads.Contains(method.Name)
            || method.ContainingType?.ToDisplayString() != EnvironmentType
            || IsInCompositionRoot(invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), $"Environment.{method.Name}"));
    }

    private static bool IsConfigurationType(ITypeSymbol type) =>
        type.ToDisplayString() == ConfigurationInterface
        || type.AllInterfaces.Any(i => i.ToDisplayString() == ConfigurationInterface);

    private static bool IsInCompositionRoot(SyntaxNode node)
    {
        if (node.Ancestors().OfType<GlobalStatementSyntax>().Any())
        {
            return true;
        }

        if (node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>()
            .Any(ns => ns.Name.ToString().StartsWith(ConfigurationNamespace, StringComparison.Ordinal)))
        {
            return true;
        }

        var outermostType = node.Ancestors().OfType<TypeDeclarationSyntax>().LastOrDefault();
        return outermostType is not null
            && outermostType.Modifiers.Any(SyntaxKind.StaticKeyword)
            && outermostType.Identifier.Text.EndsWith(CompositionRootSuffix, StringComparison.Ordinal);
    }
}
