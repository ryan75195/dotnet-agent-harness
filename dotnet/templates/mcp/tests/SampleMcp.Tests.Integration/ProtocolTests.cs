using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;

namespace SampleMcp.Tests.Integration;

[TestFixture]
public class ProtocolTests
{
    private static async Task<McpClient> ConnectClient(WebApplicationFactory<Program> factory)
    {
        HttpClient http = factory.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = http.BaseAddress! },
            http, NullLoggerFactory.Instance, false);
        return await McpClient.CreateAsync(transport);
    }

    [Test]
    public async Task Should_list_the_sample_tool_resource_and_prompt()
    {
        using var factory = new WebApplicationFactory<Program>();
        await using var client = await ConnectClient(factory);

        var tools = await client.ListToolsAsync();
        var resources = await client.ListResourcesAsync();
        var prompts = await client.ListPromptsAsync();

        tools.Select(t => t.Name).Should().Contain("echo");
        resources.Should().NotBeEmpty();
        prompts.Should().NotBeEmpty();
    }

    [Test]
    public async Task Should_call_the_echo_tool_over_the_protocol()
    {
        using var factory = new WebApplicationFactory<Program>();
        await using var client = await ConnectClient(factory);

        var result = await client.CallToolAsync(
            "echo", new Dictionary<string, object?> { ["name"] = "Ada" });

        result.Should().NotBeNull();
    }
}
