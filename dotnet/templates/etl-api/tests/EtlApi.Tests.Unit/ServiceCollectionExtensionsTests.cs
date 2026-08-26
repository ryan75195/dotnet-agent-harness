using EtlApi.Core;
using Microsoft.Extensions.DependencyInjection;

namespace EtlApi.Tests.Unit;

[TestFixture]
public class ServiceCollectionExtensionsTests
{
    [Test]
    public void Should_return_the_same_collection_so_registrations_can_be_chained()
    {
        var services = new ServiceCollection();

        var result = services.AddCoreServices();

        Assert.That(result, Is.SameAs(services));
    }
}
