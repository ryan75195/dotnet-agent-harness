using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SampleMcp.Server.Resources;

[McpServerResourceType]
public sealed class GreetingResource
{
    [McpServerResource(UriTemplate = "mcp://greeting", Name = "greeting", MimeType = "text/plain")]
    [Description("A sample greeting resource served by this MCP server.")]
    public string Read() => "Hello from the sample MCP resource.";
}
