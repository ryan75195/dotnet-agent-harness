using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SampleMcp.Server.Prompts;

[McpServerPromptType]
public sealed class SummarizePrompt
{
    [McpServerPrompt(Name = "summarize"), Description("Builds a prompt asking the model to summarize the given text.")]
    public string Build([Description("The text to summarize.")] string text) => $"Please summarize the following text:\n\n{text}";
}
