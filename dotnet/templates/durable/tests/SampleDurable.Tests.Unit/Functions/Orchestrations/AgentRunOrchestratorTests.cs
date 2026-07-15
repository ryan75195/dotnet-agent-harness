using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.DurableTask;
using NSubstitute;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Activities;
using SampleDurable.Functions.Orchestrations;

namespace SampleDurable.Tests.Unit.Functions.Orchestrations;

[TestFixture]
public class AgentRunOrchestratorTests
{
    [Test]
    public void Should_derive_the_sub_instance_id_from_the_run_key_and_work_item_id()
    {
        var instanceId = AgentRunOrchestrator.SubInstanceId("run-key-1", "item-1");

        instanceId.Should().Be("run-run-key-1-item-1");
    }

    [Test]
    public void Should_derive_distinct_sub_instance_ids_for_distinct_work_items()
    {
        var first = AgentRunOrchestrator.SubInstanceId("run-key-1", "item-1");
        var second = AgentRunOrchestrator.SubInstanceId("run-key-1", "item-2");

        first.Should().NotBe(second);
    }

    [Test]
    public async Task Should_aggregate_sub_orchestration_results_into_a_summary()
    {
        var context = Substitute.For<TaskOrchestrationContext>();

        context
            .CallSubOrchestratorAsync<AgentResult>(
                Arg.Is<TaskName>(n => n.Name == nameof(AgentTaskOrchestrator)),
                Arg.Any<object>(),
                Arg.Any<TaskOptions>())
            .Returns(
                Task.FromResult(new AgentResult("a", true, "ok")),
                Task.FromResult(new AgentResult("b", false, "timed out")));

        var request = new AgentRunRequest("key-1",
        [
            new AgentWorkItem("a", "p"),
            new AgentWorkItem("b", "p")
        ]);

        var summary = await AgentRunOrchestrator.Run(context, request);

        using (new AssertionScope())
        {
            summary.Total.Should().Be(2);
            summary.Succeeded.Should().Be(1);
            summary.TimedOut.Should().Be(1);
        }
    }

    [Test]
    public async Task Should_publish_the_summary()
    {
        var context = Substitute.For<TaskOrchestrationContext>();
        context
            .CallSubOrchestratorAsync<AgentResult>(
                Arg.Any<TaskName>(), Arg.Any<object>(), Arg.Any<TaskOptions>())
            .Returns(Task.FromResult(new AgentResult("a", true, "ok")));

        var request = new AgentRunRequest("key-1", [new AgentWorkItem("a", "p")]);

        await AgentRunOrchestrator.Run(context, request);

        await context.Received(1).CallActivityAsync(
            Arg.Is<TaskName>(n => n.Name == nameof(PublishSummaryActivity)),
            Arg.Any<object>(),
            Arg.Any<TaskOptions>());
    }
}
