using SampleDurable.Core.Models;

namespace SampleDurable.Core.Interfaces;

public interface IAgentDispatcher
{
    Task<AgentDispatch> Dispatch(AgentWorkItem workItem, CancellationToken cancellationToken);
}
