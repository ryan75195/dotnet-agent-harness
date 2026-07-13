using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace SampleMcp.Tests.Architecture;

[TestFixture]
public class LayerDependencyTests
{
    [Test]
    public void Should_keep_models_free_of_data_layer_dependencies()
    {
        Types.InAssembly(TestHelpers.CoreAssembly)
            .That().ResideInNamespaceContaining("Core.Models")
            .ShouldNot().HaveDependencyOnAny(
                "SampleMcp.Core.Data",
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
                "SampleMcp.Core.Data",
                "Microsoft.EntityFrameworkCore")
            .GetResult().IsSuccessful.Should().BeTrue(
                "Interfaces must not depend on concrete data implementations");
    }

    [Test]
    public void Should_keep_server_depending_only_on_first_party_core()
    {
        var serverAssembly = Assembly.Load("SampleMcp.Server");
        var firstPartyRefs = serverAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name?.StartsWith("SampleMcp", StringComparison.Ordinal) == true)
            .ToList();

        firstPartyRefs.Should().BeEquivalentTo(new[] { "SampleMcp.Core" },
            "Server is the sole entry point and may only reference Core within first-party assemblies");
    }
}
