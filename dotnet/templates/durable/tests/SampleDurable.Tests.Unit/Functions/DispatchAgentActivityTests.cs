using FluentAssertions;
using NSubstitute;
using SampleDurable.Core.Interfaces;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Activities;

namespace SampleDurable.Tests.Unit.Functions;

[TestFixture]
public class DispatchAgentActivityTests
{
    [Test]
    public async Task Should_return_the_dispatch_produced_by_the_dispatcher()
    {
        var dispatcher = Substitute.For<IAgentDispatcher>();
        var item = new AgentWorkItem("item-1", "do the thing");
        dispatcher
            .Dispatch(item, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AgentDispatch("dispatch-9", "item-1")));

        var activity = new DispatchAgentActivity(dispatcher);

        var result = await activity.Run(item, CancellationToken.None);

        result.DispatchId.Should().Be("dispatch-9");
    }
}
