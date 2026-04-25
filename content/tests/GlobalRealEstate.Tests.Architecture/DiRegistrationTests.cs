using FluentAssertions;
using GlobalRealEstate.Core;
using Microsoft.Extensions.DependencyInjection;

namespace GlobalRealEstate.Tests.Architecture;

[TestFixture]
public class DiRegistrationTests
{
    [Test]
    public void Should_register_every_public_core_interface_in_add_core_services()
    {
        var services = new ServiceCollection();
        services.AddCoreServices();
        var provider = services.BuildServiceProvider();

        var coreInterfaces = TestHelpers.CoreAssembly.GetTypes()
            .Where(t => t.IsInterface
                && t.IsPublic
                && t.Namespace?.StartsWith("GlobalRealEstate.Core", StringComparison.Ordinal) == true)
            .ToList();

        var missing = coreInterfaces
            .Where(iface => provider.GetService(iface) == null)
            .Select(iface => $"  {iface.FullName}")
            .ToList();

        missing.Should().BeEmpty(
            $"Every public Core interface must be registered in AddCoreServices(). " +
            $"Missing ({missing.Count}):\n{string.Join("\n", missing)}");
    }
}
