using System.Reflection;
using FluentAssertions;
using FluentAssertions.Execution;
using NetArchTest.Rules;

namespace SampleDurable.Tests.Architecture;

[TestFixture]
public class DurableFunctionTests
{
    [Test]
    public void Should_keep_orchestrations_free_of_core_behaviour()
    {
        Types.InAssembly(TestHelpers.FunctionsAssembly)
            .That().ResideInNamespaceContaining("Functions.Orchestrations")
            .ShouldNot().HaveDependencyOnAny(
                "SampleDurable.Core.Interfaces",
                "SampleDurable.Core.Services")
            .GetResult().IsSuccessful.Should().BeTrue(
                "Orchestrations may see data (Core.Models) but never behaviour — reaching a service from an orchestrator breaks replay");
    }

    [Test]
    public void Should_confine_each_trigger_type_to_its_own_namespace()
    {
        var violations = new List<string>();

        foreach (var method in TestHelpers.FunctionMethods())
        {
            var ns = method.DeclaringType?.Namespace ?? string.Empty;

            foreach (var (attribute, expected) in TestHelpers.TriggerNamespaces)
            {
                var hasTrigger = method.GetParameters()
                    .Any(p => p.GetCustomAttributes()
                        .Any(a => a.GetType().Name == attribute));

                if (hasTrigger && !ns.EndsWith(expected, StringComparison.Ordinal))
                {
                    violations.Add($"{method.DeclaringType?.Name}.{method.Name} has {attribute} but lives in {ns}, expected a namespace ending in {expected}");
                }
            }
        }

        violations.Should().BeEmpty(
            "each trigger type belongs in its own folder so the guardrails and the reader can find them");
    }

    [Test]
    public void Should_declare_at_least_one_of_each_trigger_type()
    {
        var methods = TestHelpers.FunctionMethods().ToList();

        bool HasTrigger(string attribute) => methods.Any(m =>
            m.GetParameters().Any(p => p.GetCustomAttributes().Any(a => a.GetType().Name == attribute)));

        using (new AssertionScope())
        {
            HasTrigger("OrchestrationTriggerAttribute").Should().BeTrue();
            HasTrigger("ActivityTriggerAttribute").Should().BeTrue();
            HasTrigger("EntityTriggerAttribute").Should().BeTrue();
            HasTrigger("HttpTriggerAttribute").Should().BeTrue();
        }
    }
}
