using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace ConsoleApp.Tests.Architecture;

[TestFixture]
public class LayerDependencyTests
{
    [Test]
    public void Should_keep_models_free_of_data_layer_dependencies()
    {
        Types.InAssembly(TestHelpers.CoreAssembly)
            .That().ResideInNamespaceContaining("Core.Models")
            .ShouldNot().HaveDependencyOnAny(
                "ConsoleApp.Core.Data",
                "Microsoft.EntityFrameworkCore")
            .GetResult().IsSuccessful.Should().BeTrue(
                "Domain models must be pure records with no EF or data layer dependencies");
    }

    [Test]
    public void Should_keep_interfaces_free_of_data_implementation_dependencies()
    {
        Types.InAssembly(TestHelpers.CoreAssembly)
            .That().ResideInNamespaceContaining("Core.Interfaces")
            .ShouldNot().HaveDependencyOnAny(
                "ConsoleApp.Core.Data",
                "Microsoft.EntityFrameworkCore")
            .GetResult().IsSuccessful.Should().BeTrue(
                "Interfaces must not depend on concrete data implementations");
    }

    [Test]
    public void Should_keep_cli_depending_only_on_first_party_core()
    {
        var cliAssembly = Assembly.Load("ConsoleApp.Cli");
        var firstPartyRefs = cliAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name?.StartsWith("ConsoleApp", StringComparison.Ordinal) == true)
            .ToList();

        firstPartyRefs.Should().BeEquivalentTo(new[] { "ConsoleApp.Core" },
            "Cli is the sole entry point and may only reference Core within first-party assemblies");
    }
}
