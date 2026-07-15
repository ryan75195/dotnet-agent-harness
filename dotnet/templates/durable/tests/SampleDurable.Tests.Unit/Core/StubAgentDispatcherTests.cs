using FluentAssertions;
using FluentAssertions.Execution;
using SampleDurable.Core.Models;
using SampleDurable.Core.Services;

namespace SampleDurable.Tests.Unit.Core;

[TestFixture]
public class StubAgentDispatcherTests
{
    [Test]
    public async Task Should_return_dispatch_carrying_the_work_item_id()
    {
        var dispatcher = new StubAgentDispatcher();
        var item = new AgentWorkItem("item-7", "summarize the issue");

        var dispatch = await dispatcher.Dispatch(item, CancellationToken.None);

        using (new AssertionScope())
        {
            dispatch.WorkItemId.Should().Be("item-7");
            dispatch.DispatchId.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Test]
    public async Task Should_return_distinct_dispatch_ids_for_distinct_items()
    {
        var dispatcher = new StubAgentDispatcher();

        var first = await dispatcher.Dispatch(new AgentWorkItem("a", "p"), CancellationToken.None);
        var second = await dispatcher.Dispatch(new AgentWorkItem("b", "p"), CancellationToken.None);

        first.DispatchId.Should().NotBe(second.DispatchId);
    }
}
