using SampleDurable.Core.Interfaces;
using SampleDurable.Core.Models;

namespace SampleDurable.Core.Services;

public sealed class StubAgentDispatcher : IAgentDispatcher
{
    public Task<AgentDispatch> Dispatch(AgentWorkItem workItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        var dispatch = new AgentDispatch($"dispatch-{workItem.Id}-{Guid.NewGuid():N}", workItem.Id);
        return Task.FromResult(dispatch);
    }
}
