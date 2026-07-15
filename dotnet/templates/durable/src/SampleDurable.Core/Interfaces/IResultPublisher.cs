using SampleDurable.Core.Models;

namespace SampleDurable.Core.Interfaces;

public interface IResultPublisher
{
    Task Publish(AgentRunSummary summary, CancellationToken cancellationToken);
}
