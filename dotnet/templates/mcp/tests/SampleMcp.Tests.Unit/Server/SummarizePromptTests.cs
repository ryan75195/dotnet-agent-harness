using FluentAssertions;
using SampleMcp.Server.Prompts;

namespace SampleMcp.Tests.Unit.Server;

[TestFixture]
public class SummarizePromptTests
{
    [Test]
    public void Should_build_a_summarize_prompt_containing_the_text()
    {
        var prompt = new SummarizePrompt();

        var result = prompt.Build("hello world");

        result.Should().Contain("hello world");
    }
}
