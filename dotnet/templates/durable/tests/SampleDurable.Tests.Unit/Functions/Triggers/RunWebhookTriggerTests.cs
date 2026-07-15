using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using NSubstitute;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Triggers;

namespace SampleDurable.Tests.Unit.Functions.Triggers;

[TestFixture]
public class RunWebhookTriggerTests
{
    [Test]
    public async Task Should_schedule_a_new_run_with_a_deterministic_instance_id()
    {
        var client = Substitute.For<DurableTaskClient>("test");
        client.GetInstanceAsync("run-run-key-1", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((OrchestrationMetadata?)null);
        var request = new DefaultHttpContext().Request;
        var body = new AgentRunRequest("run-key-1", [new AgentWorkItem("item-1", "do the thing")]);

        var result = await RunWebhookTrigger.Run(request, client, body);

        await client.Received(1).ScheduleNewOrchestrationInstanceAsync(
            "AgentRunOrchestrator",
            body,
            Arg.Is<StartOrchestrationOptions>(o => o.InstanceId == "run-run-key-1"));
        result.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task Should_return_the_existing_run_without_scheduling_a_duplicate()
    {
        var client = Substitute.For<DurableTaskClient>("test");
        var existing = new OrchestrationMetadata("AgentRunOrchestrator", "run-run-key-1");
        client.GetInstanceAsync("run-run-key-1", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(existing);
        var request = new DefaultHttpContext().Request;
        var body = new AgentRunRequest("run-key-1", [new AgentWorkItem("item-1", "do the thing")]);

        var result = await RunWebhookTrigger.Run(request, client, body);

        await client.DidNotReceive().ScheduleNewOrchestrationInstanceAsync(
            Arg.Any<TaskName>(), Arg.Any<object>(), Arg.Any<StartOrchestrationOptions>());
        result.Should().BeOfType<OkObjectResult>();
    }
}
