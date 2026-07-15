using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DurableTask.Client;
using NSubstitute;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Orchestrations;
using SampleDurable.Functions.Triggers;

namespace SampleDurable.Tests.Unit.Functions.Triggers;

[TestFixture]
public class CallbackTriggerTests
{
    [Test]
    public async Task Should_raise_the_agent_completed_event_on_the_named_instance()
    {
        var client = Substitute.For<DurableTaskClient>("test");
        var request = new DefaultHttpContext().Request;
        var result = new AgentResult("item-1", true, "done");

        var response = await CallbackTrigger.Run(request, client, "run-run-key-1-item-1", result);

        await client.Received(1).RaiseEventAsync(
            "run-run-key-1-item-1",
            AgentTaskOrchestrator.AgentCompletedEventName,
            result,
            Arg.Any<CancellationToken>());
        response.Should().BeOfType<AcceptedResult>();
    }
}
