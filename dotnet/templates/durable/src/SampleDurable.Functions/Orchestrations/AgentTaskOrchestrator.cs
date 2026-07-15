using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Activities;
using SampleDurable.Functions.Entities;

namespace SampleDurable.Functions.Orchestrations;

public static class AgentTaskOrchestrator
{
    public const string AgentCompletedEventName = "AgentCompleted";

    public static readonly TimeSpan DispatchTimeout = TimeSpan.FromHours(2);

    [Function(nameof(AgentTaskOrchestrator))]
    public static async Task<AgentResult> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context,
        AgentWorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(workItem);

        var counter = new EntityInstanceId(nameof(RunCounterEntity), context.InstanceId);

        await context.CallActivityAsync<AgentDispatch>(nameof(DispatchAgentActivity), workItem);
        await context.Entities.SignalEntityAsync(counter, nameof(RunCounterEntity.Dispatched));

        using var timeoutCts = new CancellationTokenSource();
        var completed = context.WaitForExternalEvent<AgentResult>(AgentCompletedEventName);
        var timeout = context.CreateTimer(context.CurrentUtcDateTime.Add(DispatchTimeout), timeoutCts.Token);

        var winner = await Task.WhenAny(completed, timeout);

        if (winner == completed)
        {
            _ = timeoutCts.CancelAsync();
            await context.Entities.SignalEntityAsync(counter, nameof(RunCounterEntity.Completed));
            return await completed;
        }

        await context.Entities.SignalEntityAsync(counter, nameof(RunCounterEntity.TimedOut));
        return new AgentResult(workItem.Id, Succeeded: false, Output: "timed out");
    }
}
