using Microsoft.Azure.Functions.Worker;
using SampleDurable.Core.Interfaces;
using SampleDurable.Core.Models;

namespace SampleDurable.Functions.Activities;

public sealed class DispatchAgentActivity
{
    private readonly IAgentDispatcher _dispatcher;

    public DispatchAgentActivity(IAgentDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [Function(nameof(DispatchAgentActivity))]
    public Task<AgentDispatch> Run(
        [ActivityTrigger] AgentWorkItem workItem,
        CancellationToken cancellationToken)
        => _dispatcher.Dispatch(workItem, cancellationToken);
}
