using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using NSubstitute;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Activities;
using SampleDurable.Functions.Orchestrations;

namespace SampleDurable.Tests.Unit.Functions.Orchestrations;

[TestFixture]
public class AgentTaskOrchestratorTests
{
    private static TaskOrchestrationContext CreateContext()
    {
        var context = Substitute.For<TaskOrchestrationContext>();
        context.CurrentUtcDateTime.Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        context.InstanceId.Returns("run-abc");
        context.Entities.Returns(Substitute.For<TaskOrchestrationEntityFeature>());
        return context;
    }

    private static void StubDispatch(TaskOrchestrationContext context)
    {
        context
            .CallActivityAsync<AgentDispatch>(
                Arg.Is<TaskName>(n => n.Name == nameof(DispatchAgentActivity)),
                Arg.Any<object>(),
                Arg.Any<TaskOptions>())
            .Returns(Task.FromResult(new AgentDispatch("d-1", "item-1")));
    }

    [Test]
    public async Task Should_return_timed_out_result_when_the_timer_wins()
    {
        var context = CreateContext();
        StubDispatch(context);

        context
            .WaitForExternalEvent<AgentResult>(AgentTaskOrchestrator.AgentCompletedEventName)
            .Returns(new TaskCompletionSource<AgentResult>().Task);
        context
            .CreateTimer(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await AgentTaskOrchestrator.Run(context, new AgentWorkItem("item-1", "p"));

        using (new AssertionScope())
        {
            result.Succeeded.Should().BeFalse();
            result.WorkItemId.Should().Be("item-1");
            result.Output.Should().Be("timed out");
        }
    }

    [Test]
    public async Task Should_return_the_agent_result_when_the_callback_wins()
    {
        var context = CreateContext();
        StubDispatch(context);

        context
            .WaitForExternalEvent<AgentResult>(AgentTaskOrchestrator.AgentCompletedEventName)
            .Returns(Task.FromResult(new AgentResult("item-1", true, "done")));
        context
            .CreateTimer(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource().Task);

        var result = await AgentTaskOrchestrator.Run(context, new AgentWorkItem("item-1", "p"));

        using (new AssertionScope())
        {
            result.Succeeded.Should().BeTrue();
            result.Output.Should().Be("done");
        }
    }

    [Test]
    public async Task Should_dispatch_before_waiting_for_the_callback()
    {
        var context = CreateContext();
        StubDispatch(context);
        context
            .WaitForExternalEvent<AgentResult>(AgentTaskOrchestrator.AgentCompletedEventName)
            .Returns(Task.FromResult(new AgentResult("item-1", true, "done")));
        context.CreateTimer(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource().Task);

        await AgentTaskOrchestrator.Run(context, new AgentWorkItem("item-1", "p"));

        await context.Received(1).CallActivityAsync<AgentDispatch>(
            Arg.Is<TaskName>(n => n.Name == nameof(DispatchAgentActivity)),
            Arg.Any<object>(),
            Arg.Any<TaskOptions>());
    }
}
