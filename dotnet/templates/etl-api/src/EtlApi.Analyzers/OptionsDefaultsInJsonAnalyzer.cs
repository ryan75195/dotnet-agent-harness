using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EtlApi.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class OptionsDefaultsInJsonAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0021";

    public const string OptOutProperty = "build_property.HarnessOptionsDefaultsInCode";

    private const string SectionNameField = "SectionName";
    private const string OptionsSuffix = "Options";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Options default set in code",
        "'{0}.{1}' has a default in code — put the value in appsettings.json under '{2}' instead (a packaged SDK library opts out with <HarnessOptionsDefaultsInCode>true</HarnessOptionsDefaultsInCode>)",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (IsOptedOut(start.Options.AnalyzerConfigOptionsProvider))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
            start.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
        });
    }

    private static bool IsOptedOut(AnalyzerConfigOptionsProvider provider) =>
        provider.GlobalOptions.TryGetValue(OptOutProperty, out var value)
        && string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (property.Initializer is null || property.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return;
        }

        Report(context, property.Initializer.Value, property.Identifier.Text);
    }

    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        if (field.Modifiers.Any(SyntaxKind.ConstKeyword) || field.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return;
        }

        foreach (var variable in field.Declaration.Variables.Where(v => v.Initializer is not null))
        {
            Report(context, variable.Initializer!.Value, variable.Identifier.Text);
        }
    }

    private static void Report(SyntaxNodeAnalysisContext context, ExpressionSyntax value, string memberName)
    {
        if (IsEmptyValue(value, context.SemanticModel))
        {
            return;
        }

        if (context.ContainingSymbol?.ContainingType is not INamedTypeSymbol type)
        {
            return;
        }

        var sectionName = SectionNameOf(type);
        if (sectionName is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, value.GetLocation(), type.Name, memberName, sectionName));
    }

    private static string? SectionNameOf(INamedTypeSymbol type)
    {
        if (!type.Name.EndsWith(OptionsSuffix, StringComparison.Ordinal))
        {
            return null;
        }

        return type.GetMembers(SectionNameField)
            .OfType<IFieldSymbol>()
            .Where(f => f.IsConst && f.Type.SpecialType == SpecialType.System_String)
            .Select(f => f.ConstantValue as string)
            .FirstOrDefault();
    }

    private static bool IsEmptyValue(ExpressionSyntax value, SemanticModel model)
    {
        switch (value)
        {
            case PostfixUnaryExpressionSyntax suppressed when suppressed.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                return IsEmptyValue(suppressed.Operand, model);
            case LiteralExpressionSyntax literal:
                return literal.IsKind(SyntaxKind.NullLiteralExpression)
                    || literal.IsKind(SyntaxKind.DefaultLiteralExpression)
                    || (literal.IsKind(SyntaxKind.StringLiteralExpression) && literal.Token.ValueText.Length == 0);
            case CollectionExpressionSyntax collection:
                return collection.Elements.Count == 0;
            case BaseObjectCreationExpressionSyntax creation:
                return (creation.ArgumentList is null || creation.ArgumentList.Arguments.Count == 0)
                    && (creation.Initializer is null || creation.Initializer.Expressions.Count == 0);
            case MemberAccessExpressionSyntax or InvocationExpressionSyntax:
                return IsKnownEmpty(model.GetSymbolInfo(value).Symbol);
            default:
                return false;
        }
    }

    private static bool IsKnownEmpty(ISymbol? symbol) =>
        symbol switch
        {
            IFieldSymbol field => field.Name == "Empty" && field.ContainingType.SpecialType == SpecialType.System_String,
            IMethodSymbol method => method.Name == "Empty" && method.ContainingType.ToDisplayString() == "System.Array",
            _ => false,
        };
}
