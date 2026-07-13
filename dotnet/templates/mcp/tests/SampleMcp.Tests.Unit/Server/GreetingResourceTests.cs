using FluentAssertions;
using SampleMcp.Server.Resources;

namespace SampleMcp.Tests.Unit.Server;

[TestFixture]
public class GreetingResourceTests
{
    [Test]
    public void Should_return_the_sample_greeting_text()
    {
        var resource = new GreetingResource();

        var result = resource.Read();

        result.Should().Contain("sample MCP resource");
    }
}
