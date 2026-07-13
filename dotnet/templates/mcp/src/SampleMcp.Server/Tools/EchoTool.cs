using System.ComponentModel;
using SampleMcp.Core.Interfaces;
using ModelContextProtocol.Server;

namespace SampleMcp.Server.Tools;

[McpServerToolType]
public sealed class EchoTool(IGreetingService greeting)
{
    [McpServerTool(Name = "echo"), Description("Greets the supplied name via the Core greeting service.")]
    public string Echo([Description("The name to greet.")] string name) => greeting.Greet(name);
}
