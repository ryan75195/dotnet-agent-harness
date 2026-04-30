using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EtlApi.Analyzers;

/// <summary>
/// CI0003: Flags constructors that instantiate IDisposable/IAsyncDisposable types
/// instead of accepting them via dependency injection.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ConstructorDependencyAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0003";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Constructor creates disposable dependency",
        "'{0}' creates '{1}' in its constructor — inject it via an interface instead",
        "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly string[] AllowedNamespacePrefixes =
    {
        "System.Threading",
        "System.Collections"
    };

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

        var body = (SyntaxNode?)constructor.Body ?? constructor.ExpressionBody;
        if (body == null)
        {
            return;
        }

        if (constructor.Parent is not ClassDeclarationSyntax classDecl)
        {
            return;
        }

        var creations = body.DescendantNodes()
            .Where(n => !IsInsideLambdaOrLocalFunction(n, body))
            .Where(n => n is ObjectCreationExpressionSyntax
                || n is ImplicitObjectCreationExpressionSyntax);

        foreach (var creation in creations)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(creation);
            var createdType = typeInfo.Type;
            if (createdType == null)
            {
                continue;
            }

            if (!ImplementsDisposable(createdType))
            {
                continue;
            }

            if (IsAllowedNamespace(createdType))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(Rule, creation.GetLocation(),
                    classDecl.Identifier.Text, createdType.Name));
        }
    }

    private static bool IsInsideLambdaOrLocalFunction(SyntaxNode node, SyntaxNode boundary)
    {
        var current = node.Parent;
        while (current != null && current != boundary)
        {
            if (current is AnonymousFunctionExpressionSyntax
                || current is LocalFunctionStatementSyntax)
            {
                return true;
            }

            current = current.Parent;
        }
        return false;
    }

    private static bool ImplementsDisposable(ITypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.Name == "IDisposable" || iface.Name == "IAsyncDisposable")
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsAllowedNamespace(ITypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString();
        if (ns == null)
        {
            return false;
        }

        foreach (var prefix in AllowedNamespacePrefixes)
        {
            if (ns.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
