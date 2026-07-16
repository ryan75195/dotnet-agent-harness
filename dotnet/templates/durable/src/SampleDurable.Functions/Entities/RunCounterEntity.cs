using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;
using SampleDurable.Core.Models;

namespace SampleDurable.Functions.Entities;

public sealed class RunCounterEntity : TaskEntity<RunCounterState>
{
    protected override RunCounterState InitializeState(TaskEntityOperation entityOperation)
        => RunCounterState.Empty;

    public void Dispatched() => State = State with { Dispatched = State.Dispatched + 1 };

    public void Completed() => State = State with { Completed = State.Completed + 1 };

    public void TimedOut() => State = State with { TimedOut = State.TimedOut + 1 };

    public RunCounterState Get() => State;

    [Function(nameof(RunCounterEntity))]
    public Task Run([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        return dispatcher.DispatchAsync(this);
    }
}
