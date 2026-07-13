using FluentAssertions;
using SampleMcp.Core.Services;
using SampleMcp.Server.Tools;

namespace SampleMcp.Tests.Unit.Server;

[TestFixture]
public class EchoToolTests
{
    [Test]
    public void Should_echo_a_greeting_for_the_name()
    {
        var tool = new EchoTool(new GreetingService());

        var result = tool.Echo("Ada");

        result.Should().Be("Hello, Ada!");
    }
}
