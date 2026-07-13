using FluentAssertions;
using SampleMcp.Core.Services;

namespace SampleMcp.Tests.Unit.Core;

[TestFixture]
public class GreetingServiceTests
{
    [Test]
    public void Should_greet_the_given_name()
    {
        var service = new GreetingService();

        var result = service.Greet("Ada");

        result.Should().Be("Hello, Ada!");
    }
}
