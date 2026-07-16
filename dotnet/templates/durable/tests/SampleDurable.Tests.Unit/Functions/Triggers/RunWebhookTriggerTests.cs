using System.Text;
using System.Text.Json;
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
    private static HttpRequest CreateJsonRequest(AgentRunRequest body)
    {
        var request = new DefaultHttpContext().Request;
        var json = JsonSerializer.Serialize(body, JsonSerializerOptions.Web);
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        request.ContentType = "application/json";
        request.ContentLength = request.Body.Length;
        return request;
    }

    [Test]
    public async Task Should_schedule_a_new_run_with_a_deterministic_instance_id()
    {
        var client = Substitute.For<DurableTaskClient>("test");
        client.GetInstanceAsync("run-run-key-1", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((OrchestrationMetadata?)null);
        var body = new AgentRunRequest("run-key-1", [new AgentWorkItem("item-1", "do the thing")]);
        var request = CreateJsonRequest(body);

        var result = await RunWebhookTrigger.Run(request, client);

        await client.Received(1).ScheduleNewOrchestrationInstanceAsync(
            "AgentRunOrchestrator",
            Arg.Is<AgentRunRequest>(b =>
                b.RunKey == body.RunKey
                && b.Items.Count == 1
                && b.Items[0].Id == "item-1"
                && b.Items[0].Prompt == "do the thing"),
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
        var body = new AgentRunRequest("run-key-1", [new AgentWorkItem("item-1", "do the thing")]);
        var request = CreateJsonRequest(body);

        var result = await RunWebhookTrigger.Run(request, client);

        await client.DidNotReceive().ScheduleNewOrchestrationInstanceAsync(
            Arg.Any<TaskName>(), Arg.Any<object>(), Arg.Any<StartOrchestrationOptions>());
        result.Should().BeOfType<OkObjectResult>();
    }

    [TestCase(OrchestrationRuntimeStatus.Completed)]
    [TestCase(OrchestrationRuntimeStatus.Failed)]
    [TestCase(OrchestrationRuntimeStatus.Terminated)]
    public async Task Should_start_a_fresh_run_when_the_previous_run_reached_a_terminal_state(
        OrchestrationRuntimeStatus terminalStatus)
    {
        var client = Substitute.For<DurableTaskClient>("test");
        var existing = new OrchestrationMetadata("AgentRunOrchestrator", "run-run-key-1")
        {
            RuntimeStatus = terminalStatus,
        };
        client.GetInstanceAsync("run-run-key-1", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(existing);
        var body = new AgentRunRequest("run-key-1", [new AgentWorkItem("item-1", "do the thing")]);
        var request = CreateJsonRequest(body);

        var result = await RunWebhookTrigger.Run(request, client);

        await client.Received(1).ScheduleNewOrchestrationInstanceAsync(
            "AgentRunOrchestrator",
            Arg.Any<AgentRunRequest>(),
            Arg.Is<StartOrchestrationOptions>(o => o.InstanceId == "run-run-key-1"));
        result.Should().BeOfType<OkObjectResult>();
    }

    [TestCase(OrchestrationRuntimeStatus.Running)]
    [TestCase(OrchestrationRuntimeStatus.Pending)]
    [TestCase(OrchestrationRuntimeStatus.Suspended)]
    public async Task Should_not_schedule_a_duplicate_while_the_existing_run_is_still_in_flight(
        OrchestrationRuntimeStatus inFlightStatus)
    {
        var client = Substitute.For<DurableTaskClient>("test");
        var existing = new OrchestrationMetadata("AgentRunOrchestrator", "run-run-key-1")
        {
            RuntimeStatus = inFlightStatus,
        };
        client.GetInstanceAsync("run-run-key-1", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(existing);
        var body = new AgentRunRequest("run-key-1", [new AgentWorkItem("item-1", "do the thing")]);
        var request = CreateJsonRequest(body);

        var result = await RunWebhookTrigger.Run(request, client);

        await client.DidNotReceive().ScheduleNewOrchestrationInstanceAsync(
            Arg.Any<TaskName>(), Arg.Any<object>(), Arg.Any<StartOrchestrationOptions>());
        result.Should().BeOfType<OkObjectResult>();
    }
}
