using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace EtlApi.Tests.Architecture;

[TestFixture]
public class LayerDependencyTests
{
    [Test]
    public void Should_keep_models_free_of_data_layer_dependencies()
    {
        Types.InAssembly(TestHelpers.CoreAssembly)
            .That().ResideInNamespaceContaining("Core.Models")
            .ShouldNot().HaveDependencyOnAny(
                "EtlApi.Core.Data",
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
                "EtlApi.Core.Data",
                "Microsoft.EntityFrameworkCore")
            .GetResult().IsSuccessful.Should().BeTrue(
                "Interfaces must not depend on concrete data implementations");
    }

    [Test]
    public void Should_keep_api_free_of_etl_references()
    {
        var apiAssembly = Assembly.Load("EtlApi.Api");
        var referencedNames = apiAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        referencedNames.Should().NotContain("EtlApi.Etl",
            "API layer must not reference ETL — they are independent entry points");
    }

    [Test]
    public void Should_keep_etl_free_of_api_dependencies()
    {
        Types.InAssembly(TestHelpers.EtlAssembly)
            .That().ResideInNamespaceContaining("Etl")
            .ShouldNot().HaveDependencyOnAny("EtlApi.Api")
            .GetResult().IsSuccessful.Should().BeTrue(
                "ETL pipeline must not reference the API layer");
    }
}
