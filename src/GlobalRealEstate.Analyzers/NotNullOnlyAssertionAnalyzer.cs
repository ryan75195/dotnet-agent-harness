using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GlobalRealEstate.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NotNullOnlyAssertionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0009";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Test asserts non-null without verifying data",
        "Test '{0}' asserts '{1}' is not null but never verifies its data — add assertions on the actual values",
        "Testing",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (!IsTestMethod(method))
        {
            return;
        }

        if (!IsInTestFixture(method))
        {
            return;
        }

        var body = method.Body;
        if (body == null)
        {
            return;
        }

        var allAssertions = CollectAssertThatInvocations(body);
        var notNullAssertions = FindNotNullAssertions(allAssertions);

        foreach (var (variableName, location) in notNullAssertions)
        {
            if (!HasFollowUpAssertion(allAssertions, variableName, location))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, location,
                        method.Identifier.Text, variableName));
            }
        }
    }

    private static bool IsTestMethod(MethodDeclarationSyntax method)
    {
        return method.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => IsTestAttribute(a));
    }

    private static bool IsTestAttribute(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString();
        return name is "Test" or "NUnit.Framework.Test"
            or "TestCase" or "NUnit.Framework.TestCase";
    }

    private static bool IsInTestFixture(MethodDeclarationSyntax method)
    {
        var typeDecl = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (typeDecl == null)
        {
            return false;
        }

        return typeDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => IsTestFixtureAttribute(a));
    }

    private static bool IsTestFixtureAttribute(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString();
        return name is "TestFixture" or "NUnit.Framework.TestFixture";
    }

    private static ImmutableArray<InvocationExpressionSyntax> CollectAssertThatInvocations(BlockSyntax body)
    {
        return body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(IsAssertThatCall)
            .ToImmutableArray();
    }

    private static bool IsAssertThatCall(InvocationExpressionSyntax invocation)
    {
        var expression = invocation.Expression;

        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.Text == "That"
                && IsAssertClass(memberAccess.Expression.ToString());
        }

        return false;
    }

    private static bool IsAssertClass(string expression)
    {
        return expression is "Assert" or "NUnit.Framework.Assert";
    }

    private static ImmutableArray<(string VariableName, Location Location)> FindNotNullAssertions(
        ImmutableArray<InvocationExpressionSyntax> assertions)
    {
        var builder = ImmutableArray.CreateBuilder<(string, Location)>();

        foreach (var assertion in assertions)
        {
            var args = assertion.ArgumentList.Arguments;
            if (args.Count < 2)
            {
                continue;
            }

            var firstArg = args[0].Expression;
            if (firstArg is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            if (!IsNotNullConstraint(args[1].Expression))
            {
                continue;
            }

            var location = (assertion.Expression as MemberAccessExpressionSyntax)
                ?.Name.GetLocation() ?? assertion.GetLocation();

            builder.Add((identifier.Identifier.Text, location));
        }

        return builder.ToImmutable();
    }

    private static bool IsNotNullConstraint(ExpressionSyntax expression)
    {
        // Is.Not.Null -> MemberAccess(MemberAccess(Is, Not), Null)
        if (expression is MemberAccessExpressionSyntax outerAccess
            && outerAccess.Name.Identifier.Text == "Null"
            && outerAccess.Expression is MemberAccessExpressionSyntax innerAccess
            && innerAccess.Name.Identifier.Text == "Not"
            && IsIsClass(innerAccess.Expression.ToString()))
        {
            return true;
        }

        return false;
    }

    private static bool IsIsClass(string expression)
    {
        return expression is "Is" or "NUnit.Framework.Is";
    }

    private static bool HasFollowUpAssertion(
        ImmutableArray<InvocationExpressionSyntax> assertions,
        string variableName,
        Location notNullLocation)
    {
        foreach (var assertion in assertions)
        {
            var assertionLocation = assertion.GetLocation();

            // Skip if this is the not-null assertion itself
            if (assertionLocation.SourceSpan == notNullLocation.SourceSpan)
            {
                continue;
            }

            var args = assertion.ArgumentList.Arguments;
            if (args.Count < 1)
            {
                continue;
            }

            var firstArgText = args[0].Expression.ToString();

            // Check if the first argument starts with the variable name followed by . or !
            if (ReferencesVariable(firstArgText, variableName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReferencesVariable(string expressionText, string variableName)
    {
        if (expressionText.StartsWith(variableName + "."))
        {
            return true;
        }

        if (expressionText.StartsWith(variableName + "!"))
        {
            return true;
        }

        if (expressionText.StartsWith(variableName + "?"))
        {
            return true;
        }

        return false;
    }
}
